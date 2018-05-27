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
    public class FollowFunctions
    {
        const string USER_TABLE_NAME_LOOKUP = "UserTable";
        private const string USER_ID_PATH = "user_id";

        IDynamoDBContext DDBContext { get; }

        public FollowFunctions()
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

        public FollowFunctions(IAmazonDynamoDB ddbClient, string tableName)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(User)] = new Amazon.Util.TypeMapping(typeof(User), tableName);
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(Photo)] = new Amazon.Util.TypeMapping(typeof(Photo), "photoInfo");
            }

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DDBContext = new DynamoDBContext(ddbClient, config);
        }

        public async Task<APIGatewayProxyResponse> FollowAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                string userId = null;
                if (request.PathParameters != null && request.PathParameters.ContainsKey(USER_ID_PATH))
                    userId = request.PathParameters[USER_ID_PATH];

                var requestBody = JsonConvert.DeserializeObject<FollowUnfollowRequest>(request.Body);

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(requestBody.other_user_id))
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "Missing required parameter",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }
                context.Logger.LogLine($"Getting user {userId}");
                var user = await DDBContext.LoadAsync<User>(userId);
                context.Logger.LogLine($"Getting other user {requestBody.other_user_id}");
                var followed = await DDBContext.LoadAsync<User>(requestBody.other_user_id);

                if (user == null)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "User cannot be found",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                if (followed == null)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "Other user cannot be found",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }


                if (user.followed_friend_id.Contains(followed.user_id))
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "You have already follow this user",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                user.followed_friend_id.Add(followed.user_id);

                await DDBContext.SaveAsync<User>(user);

                var response = new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = JsonConvert.SerializeObject(new { requestBody.other_user_id }),
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

        public async Task<APIGatewayProxyResponse> UnFollowAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                string userId = null;
                if (request.PathParameters != null && request.PathParameters.ContainsKey(USER_ID_PATH))
                    userId = request.PathParameters[USER_ID_PATH];

                var requestBody = JsonConvert.DeserializeObject<FollowUnfollowRequest>(request.Body);

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(requestBody.other_user_id))
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "Missing required parameter",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                var user = await DDBContext.LoadAsync<User>(userId);
                var followed = await DDBContext.LoadAsync<User>(requestBody.other_user_id);

                if (user == null)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "User cannot be found",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                if (followed == null)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "Other user cannot be found",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }


                if (!user.followed_friend_id.Contains(followed.user_id))
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "You have not followed this user",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                user.followed_friend_id.RemoveAll(e => e == followed.user_id);

                await DDBContext.SaveAsync<User>(user);

                var response = new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = JsonConvert.SerializeObject(new { requestBody.other_user_id }),
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
