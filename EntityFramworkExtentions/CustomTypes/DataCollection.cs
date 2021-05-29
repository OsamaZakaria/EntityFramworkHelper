using System.Collections.Generic;

namespace EntityFramworkExtentions.CustomTypes
{
    public class DataCollection<T>
    {
        public List<T> Items { get; set; }
        public int TotalItemCount { get; set; }
        public int ThisPageItemCount { get; set; }
        public int TotalPageCount { get; set; }
    }
}
