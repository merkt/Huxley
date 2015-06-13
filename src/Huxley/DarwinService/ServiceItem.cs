namespace Huxley.DarwinService
{
    public partial class ServiceItem
    {
        public string EstimatedTimeOfArrival
        {
            get { return etaField; }
        }

        public string EstimatedTimeOfDeparture
        {
            get { return etdField; }
        }

        public string ScheduledTimeOfArrival
        {
            get { return staField; }
        }

        public string ScheduledTimeOfDeparture
        {
            get { return stdField; }
        }
    }
}