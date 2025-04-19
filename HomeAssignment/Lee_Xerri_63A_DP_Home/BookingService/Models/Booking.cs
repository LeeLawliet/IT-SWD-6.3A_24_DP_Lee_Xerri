using Google.Cloud.Firestore;

namespace BookingService.Models
{
    [FirestoreData]
    public class Booking
    {
        [FirestoreProperty] public string Id { get; set; }
        [FirestoreProperty] public string UserUid { get; set; }
        [FirestoreProperty] public string StartLocation { get; set; }
        [FirestoreProperty] public string EndLocation { get; set; }
        [FirestoreProperty] public Timestamp DateTime { get; set; }
        [FirestoreProperty] public int Passengers { get; set; }
        [FirestoreProperty] public string CabType { get; set; } // "Economic","Premium","Executive"
        [FirestoreProperty] public bool Paid { get; set; }
    }
}
