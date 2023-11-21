using System.Text.Json.Serialization;

namespace user_api_minimal.Models
{
    public class User
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }

        //This tag ignores the Email property in case it has a null value
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Email { get; set; }
        public bool MarketingConsent { get; set; }
    }
}
