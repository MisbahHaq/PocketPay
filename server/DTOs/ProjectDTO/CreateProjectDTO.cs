using server.Models;

namespace server.DTOs.ProjectDTO
{
    public class CreateProjectDTO
    {
     
        public string ProjectName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

 
    }
}
