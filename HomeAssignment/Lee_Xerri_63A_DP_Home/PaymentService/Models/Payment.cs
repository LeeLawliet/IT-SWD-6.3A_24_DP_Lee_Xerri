using Google.Cloud.Firestore;

namespace PaymentService.Models
{
    [FirestoreData]
    public class Payment
    {
        [FirestoreProperty] public string Id { get; set; }
        [FirestoreProperty] public string BookingId { get; set; }
        [FirestoreProperty] public string UserUid { get; set; }
        [FirestoreProperty] public double CabFare { get; set; } // retrieved from external API
        [FirestoreProperty] public double CabMultiplier { get; set; }
        [FirestoreProperty] public double DaytimeMultiplier { get; set; }
        [FirestoreProperty] public double PassengersMultiplier { get; set; }
        [FirestoreProperty] public double DiscountMultiplier { get; set; }
        [FirestoreProperty] public double TotalPrice { get; set; }
        [FirestoreProperty] public Timestamp CreatedAt { get; set; }
    }
}