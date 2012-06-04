namespace SEF.Contracts
{
    public struct Limit
    {
        public int Skip;
        public int Take;
        public Limit(int skip, int take)
        {
            Skip = skip;
            Take = take;
        }
    }
}