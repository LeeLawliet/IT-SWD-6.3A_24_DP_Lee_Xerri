namespace LeeXerri_CustomerService.DTO
{
    public class SignInResponse
    {
        public string idToken { get; set; }
        public string refreshToken { get; set; }
        public string expiresIn { get; set; }
        
    }
}
