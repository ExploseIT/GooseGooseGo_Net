


namespace GooseGooseGo_Net.ef
{
    public class cIsDbNull
    {
        public object IsDbNull(object? val)
        {
            if (val == null)
            {
                return DBNull.Value;
            }
            else
            {
                return val;
            }
        }

        public object IsDbEmptyString(object? val)
        {
            if (val == null)
            {
                return "";
            }
            else
            {
                return val;
            }
        }
    }
}

