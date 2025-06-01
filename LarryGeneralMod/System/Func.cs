namespace System
{
    internal class Func
    {
        private Player player;
        private CardInfo card;
        private bool v;

        public Func(Player player, CardInfo card, bool v)
        {
            this.player = player;
            this.card = card;
            this.v = v;
        }
    }
}