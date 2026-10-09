using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Authorization;
using server.Data;
using server.DTOs.Inventory;
using server.Helpers;
using server.Models.Inventory;
using server.Services;

namespace server.Controllers.Inventory
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;

        public ProductsController(AppDbContext db, ICurrentUser current, IAuditLogService audit)
        {
            _db = db;
            _current = current;
            _audit = audit;
        }

        [Permission(PermissionKeys.ViewProducts)]
        [HttpGet]
        public async Task<ActionResult> GetAll()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var products = await _db.Products.AsNoTracking()
                .Where(p => p.OrganizationId == orgId)
                .Select(p => new ProductDTO
                {
                    ProductId = p.ProductId,
                    SKU = p.SKU,
                    Barcode = p.Barcode,
                    Name = p.Name,
                    Description = p.Description,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category!.Name,
                    UnitId = p.UnitId,
                    UnitName = p.Unit!.Name,
                    CostPrice = p.CostPrice,
                    SellingPrice = p.SellingPrice,
                    MinReorderLevel = p.MinReorderLevel,
                    MaxReorderLevel = p.MaxReorderLevel,
                    IsActive = p.IsActive,
                    HasExpiry = p.HasExpiry,
                    TrackBatch = p.TrackBatch
                })
                .OrderBy(p => p.Name)
                .ToListAsync();
            return Ok(products);
        }

        [Permission(PermissionKeys.ViewProducts)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult> Get(int id)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var p = await _db.Products.AsNoTracking()
                .Where(x => x.OrganizationId == orgId && x.ProductId == id)
                .Select(x => new ProductDTO
                {
                    ProductId = x.ProductId,
                    SKU = x.SKU,
                    Barcode = x.Barcode,
                    Name = x.Name,
                    Description = x.Description,
                    CategoryId = x.CategoryId,
                    CategoryName = x.Category!.Name,
                    UnitId = x.UnitId,
                    UnitName = x.Unit!.Name,
                    CostPrice = x.CostPrice,
                    SellingPrice = x.SellingPrice,
                    MinReorderLevel = x.MinReorderLevel,
                    MaxReorderLevel = x.MaxReorderLevel,
                    IsActive = x.IsActive,
                    HasExpiry = x.HasExpiry,
                    TrackBatch = x.TrackBatch
                })
                .FirstOrDefaultAsync();
            if (p == null) return NotFound();
            return Ok(p);
        }

        [Permission(PermissionKeys.ManageProducts)]
        [HttpPost]
        public async Task<ActionResult> Create(ProductUpsertDTO dto)
        {
            var user = await _current.RequireOrgUserAsync();
            var orgId = user.OrganizationId!.Value;
            if (await _db.Products.AnyAsync(p => p.OrganizationId == orgId && p.SKU == dto.SKU))
                return BadRequest("SKU already exists.");
            var product = new Product
            {
                OrganizationId = orgId,
                SKU = dto.SKU.Trim(),
                Barcode = dto.Barcode,
                Name = dto.Name.Trim(),
                Description = dto.Description,
                CategoryId = dto.CategoryId,
                UnitId = dto.UnitId,
                CostPrice = dto.CostPrice,
                SellingPrice = dto.SellingPrice,
                MinReorderLevel = dto.MinReorderLevel,
                MaxReorderLevel = dto.MaxReorderLevel,
                IsActive = dto.IsActive,
                HasExpiry = dto.HasExpiry,
                TrackBatch = dto.TrackBatch,
                CreatedById = user.UserId,
                CreatedAt = DateTime.UtcNow
            };
            _db.Products.Add(product);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("product.created", "Product", product.ProductId.ToString(), new { product.SKU, product.Name });
            return Ok(new { product.ProductId });
        }

        [Permission(PermissionKeys.ManageProducts)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult> Update(int id, ProductUpsertDTO dto)
        {
            var user = await _current.RequireOrgUserAsync();
            var orgId = user.OrganizationId!.Value;
            var product = await _db.Products.FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.ProductId == id);
            if (product == null) return NotFound();
            if (await _db.Products.AnyAsync(p => p.OrganizationId == orgId && p.ProductId != id && p.SKU == dto.SKU))
                return BadRequest("SKU already exists.");
            product.SKU = dto.SKU.Trim();
            product.Barcode = dto.Barcode;
            product.Name = dto.Name.Trim();
            product.Description = dto.Description;
            product.CategoryId = dto.CategoryId;
            product.UnitId = dto.UnitId;
            product.CostPrice = dto.CostPrice;
            product.SellingPrice = dto.SellingPrice;
            product.MinReorderLevel = dto.MinReorderLevel;
            product.MaxReorderLevel = dto.MaxReorderLevel;
            product.IsActive = dto.IsActive;
            product.HasExpiry = dto.HasExpiry;
            product.TrackBatch = dto.TrackBatch;
            product.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync("product.updated", "Product", product.ProductId.ToString(), new { product.SKU, product.Name });
            return Ok();
        }
    }
}
