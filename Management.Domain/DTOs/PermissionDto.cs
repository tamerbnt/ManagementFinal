namespace Management.Domain.DTOs
{
    public class PermissionDto
    {
        public string Name { get; set; } // "CanManageMembers"
        public bool IsGranted { get; set; }
    }
}