using Google.Cloud.Firestore;

namespace LocationService.Models
{
    [FirestoreData]
    public class Location
    {
        [FirestoreProperty] public string Id { get; set; }
        [FirestoreProperty] public string UserUid { get; set; }
        [FirestoreProperty] public string Name { get; set; }
        [FirestoreProperty] public string Address { get; set; }
        [FirestoreProperty] public double Latitude { get; set; }
        [FirestoreProperty] public double Longitude { get; set; }
        [FirestoreProperty] public Timestamp CreatedAt { get; set; }
    }
}
