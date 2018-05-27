using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Newtonsoft.Json;
using UserLambda.Request;

namespace UserLambda
{
    public class ActivityFunctions
    {
        const string USER_TABLE_NAME_LOOKUP = "UserTable";
        private const string USER_ID_PATH = "user_id";

        IDynamoDBContext DDBContext { get; }

        public ActivityFunctions()
        {
            var userTable = Environment.GetEnvironmentVariable(USER_TABLE_NAME_LOOKUP);
            if (!string.IsNullOrEmpty(userTable))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(User)] = new Amazon.Util.TypeMapping(typeof(User), userTable);
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(Photo)] = new Amazon.Util.TypeMapping(typeof(Photo), "photoInfo");
            }

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DDBContext = new DynamoDBContext(new AmazonDynamoDBClient(), config);
        }

        public ActivityFunctions(IAmazonDynamoDB ddbClient, string tableName)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(User)] = new Amazon.Util.TypeMapping(typeof(User), tableName);
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(Photo)] = new Amazon.Util.TypeMapping(typeof(Photo), "photoInfo");
            }

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DDBContext = new DynamoDBContext(ddbClient, config);
        }

        public async Task<APIGatewayProxyResponse> GetActivityAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                string userId = null;
                if (request.PathParameters != null && request.PathParameters.ContainsKey(USER_ID_PATH))
                    userId = request.PathParameters[USER_ID_PATH];

                if (string.IsNullOrEmpty(userId))
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "Missing required parameter",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                context.Logger.LogLine($"Getting user {userId}");
                var searchUser = DDBContext.ScanAsync<User>(null);
                var users = await searchUser.GetNextSetAsync();
                var user = users.FirstOrDefault(e => e.user_id == userId);
                context.Logger.LogLine($"Found user: {user != null}");

                if (user == null)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "User cannot be found",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                var searchPhoto = DDBContext.ScanAsync<Photo>(null);
                var photos = await searchPhoto.GetNextSetAsync();

                var relevantPhotos = photos.Where(e => user.followed_friend_id.Contains(e.uploaded_user_id))
                    .OrderByDescending(e => e.created_timestamp)
                    .ToList();

                var result = relevantPhotos.Select(p => new
                {
                    p.photo_id,
                    p.original_url,
                    p.uploaded_user_id,
                    uploaded_by = users.Where(u => u.user_id == p.uploaded_user_id).Select(u => new
                    {
                        u.user_id,
                        u.name,
                        u.email,
                        u.phone_no
                    }).FirstOrDefault(),
                    liked_by = users.Where(u => p.liked_user_id.Contains(u.user_id)).Select(u => new
                    {
                        u.user_id,
                        u.name,
                        u.email,
                        u.phone_no
                    }),
                    p.labels,
                    p.moderation_labels,
                    p.created_timestamp
                });


                var response = new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = JsonConvert.SerializeObject(result),
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };
                return response;
            }
            catch (Exception ex)
            {
                context.Logger.LogLine(ex.ToString());
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Body = ex.ToString(),
                    Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                };
            }
        }
    }
}
