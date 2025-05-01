using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace TravelServiceLib
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "Service1" in both code and config file together.
    public class TravelService : ITravelService
    {
        public double CoachTypeCost(int coachType, int tourLength, int numberPeople)
        {
            double totalCost = 0.0f;
            double coachCost = 0.0f;

            switch (coachType)
            {
                case 12:
                    coachCost = 150f;
                    break;
                case 21:
                    coachCost = 100f;
                    break;
                case 55:
                    coachCost = 70f;
                    break;
                default:
                    throw new Exception("Coach type does not exist.");
            }

            if (tourLength == 3 || tourLength == 7 || tourLength == 10)
            {
                totalCost = coachCost * numberPeople * tourLength;
            }
            else
            {
                throw new Exception("TourLength not supported.");
            }

            return totalCost;
        }
    }
}
