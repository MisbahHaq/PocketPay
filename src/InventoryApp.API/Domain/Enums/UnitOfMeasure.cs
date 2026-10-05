namespace InventoryApp.Domain.Enums;

/// <summary>
/// The unit a product's quantities are counted in. Stored as an int so the database never depends
/// on the order of this declaration: inserting a new unit later must not renumber the existing rows.
/// </summary>
public enum UnitOfMeasure
{
    Piece = 0,
    Box = 1,
    Kg = 2,
    Litre = 3,
    Meter = 4,
    Pack = 5,
    Set = 6
}