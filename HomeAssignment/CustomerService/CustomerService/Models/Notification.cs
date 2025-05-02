using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;

namespace CustomerService.Models
{
    [FirestoreData]
    public class Notification
    {
        [FirestoreProperty] public string Id { get; set; }
        [FirestoreProperty] public string Message { get; set; }
        [FirestoreProperty] public DateTime Timestamp { get; set; }
    }
}
