using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UserLambda
{
    public class User
    {
        public string user_id { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public string phone_no { get; set; }
        public string password { get; set; }

        public string push_id { get; set; }
        public List<string> uploaded_photo_id { get; set; }
        public List<string> liked_photo_id { get; set; }
        public List<string> followed_friend_id { get; set; }
        public DateTime created_timestamp { get; set; }
    }
}
