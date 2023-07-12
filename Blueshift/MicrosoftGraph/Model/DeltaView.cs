namespace Blueshift.MicrosoftGraph.Model
{

    public class DeltaView<TItem>
    {
        public List<TItem> Items { get; }

        public string Token { get; set; }

        public string DeltaLink { get; set; }

        public DeltaView()
        {
            this.Items = new List<TItem>();
        }
    }
}
