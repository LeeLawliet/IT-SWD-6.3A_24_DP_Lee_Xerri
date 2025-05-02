namespace CustomerService.DTO
{
    public class SignInResponseDTO
    {
        public string idToken { get; set; }
        public string refreshToken { get; set; }
        public string expiresIn { get; set; }
        public string displayName { get; set; }
    }
}
