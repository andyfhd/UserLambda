using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UserLambda
{
    public class Photo
    {
        public string photo_id { get; set; }
        public string uploaded_user_id { get; set; }
        public List<string> liked_user_id { get; set; }
        public string original_photo_id { get; set; }
        public string thumbnail_photo_id { get; set; }
        public string created_timestamp { get; set; }
    }
}
