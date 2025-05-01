namespace CustomerService.Models
{
    using Google.Cloud.Firestore;

    [FirestoreData]
    public class User
    {
        [FirestoreProperty] public string Uid { get; set; }
        [FirestoreProperty] public string Email { get; set; }
        [FirestoreProperty] public string Username { get; set; }
    }
}