using System;
using System.Collections.Generic;
using NLog;

namespace Lychen
{
    internal class Point
    {
        public int x { set; get; }
        public int y { set; get; }
    }

    public static class Extension
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static string GetTypeString(object thing)
        {
            return thing == null ? "null" : thing.GetType().ToString();
        }

        public static Type GetType(object thing)
        {
            return thing.GetType();
        }

        public static string Bresenham(int x0, int y0, int x1, int y1)
        {
            var pointList = new List<Point>();
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = (dx > dy ? dx : -dy) / 2, e2;
            for (;;)
            {
                pointList.Add(new Point { x = x0, y = y0 });
                if (x0 == x1 && y0 == y1) break;
                e2 = err;
                if (e2 > -dx)
                {
                    err -= dy;
                    x0 += sx;
                }

                if (e2 < dy)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return SimpleJson.SerializeObject(pointList);
        }
    }
}