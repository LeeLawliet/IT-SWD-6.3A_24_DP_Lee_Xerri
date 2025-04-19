using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;

namespace LeeXerri_CustomerService.Models
{
    [FirestoreData]
    public class Notification
    {
        [FirestoreProperty] public string Id { get; set; }
        [FirestoreProperty] public string Message { get; set; }
        [FirestoreProperty] public Timestamp CreatedAt { get; set; }
        [FirestoreProperty] public bool IsRead { get; set; } = false;
    }
}
