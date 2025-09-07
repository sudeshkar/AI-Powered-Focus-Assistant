namespace FocusAssistant.Models
{
    public class MLPredictionResponse
    {
        public string ActionTaken { get; set; }
        public double DistractionRisk { get; set; }
        public string InterventionId { get; set; }
        public string InterventionMessage { get; set; }
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }

        public MLPredictionResponse()
        {
            InterventionId = Guid.NewGuid().ToString();
            Timestamp = DateTime.Now;
        }
    }
}