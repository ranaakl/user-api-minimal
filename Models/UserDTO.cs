﻿namespace user_api_minimal.Models
{
    public class UserDTO
    {
        public string? Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? Email { get; set; }
        public bool MarketingConsent { get; set; }
    }
}
