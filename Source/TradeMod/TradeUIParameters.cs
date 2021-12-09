using UnityEngine;

namespace TradeUI
{
    public class TradeUIParameters
    {
        public static TradeUIParameters Singleton
        {
            get
            {
                if (m_instance == null)
                    m_instance = new TradeUIParameters();
                return m_instance;
            }
        }

        private static TradeUIParameters m_instance;

        public void Reset()
        {
            scrollPositionLeft = Vector2.zero;
            scrollPositionRight = Vector2.zero;
        }

        public bool isDrawingColonyItems;
        public Vector2 scrollPositionLeft;
        public Vector2 scrollPositionRight;
    }
}
