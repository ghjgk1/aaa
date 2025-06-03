namespace Domain
{
    public class User
    {
        public string SamAccountName { get; set; }
        public string EmployeeId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string MiddleName { get; set; }
        public string FullName { get; set; }
        public string JobTitle { get; set; }
        public string Department { get; set; }
        public string InternalPhone { get; set; }
        public string MobilePhone { get; set; }
        public string AdditionalPhone { get; set; }
        public string Email { get; set; }
        public DateTime? HireDate { get; set; }
    }       
}
