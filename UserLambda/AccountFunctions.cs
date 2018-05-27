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

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace UserLambda
{
    public class AccountFunctions
    {
        const string USER_TABLE_NAME_LOOKUP = "UserTable";
        private const string USER_ID_PATH = "user_id";

        IDynamoDBContext DDBContext { get; }

        public AccountFunctions()
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

        public AccountFunctions(IAmazonDynamoDB ddbClient, string tableName)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(User)] = new Amazon.Util.TypeMapping(typeof(User), tableName);
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(Photo)] = new Amazon.Util.TypeMapping(typeof(Photo), "photoInfo");
            }

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DDBContext = new DynamoDBContext(ddbClient, config);
        }

        public async Task<APIGatewayProxyResponse> GetUsersAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                context.Logger.LogLine("Getting users");
                var search = this.DDBContext.ScanAsync<User>(null);
                var page = await search.GetNextSetAsync();
                context.Logger.LogLine($"Found {page.Count} users");

                var response = new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = JsonConvert.SerializeObject(page),
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

        public async Task<APIGatewayProxyResponse> GetUserAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                string userId = null;
                if (request.PathParameters != null && request.PathParameters.ContainsKey(USER_ID_PATH))
                    userId = request.PathParameters[USER_ID_PATH];
                else if (request.QueryStringParameters != null &&
                         request.QueryStringParameters.ContainsKey(USER_ID_PATH))
                    userId = request.QueryStringParameters[USER_ID_PATH];

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
                var user = await DDBContext.LoadAsync<User>(userId);
                context.Logger.LogLine($"Found user: {user != null}");

                if (user == null)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.NotFound
                    };
                }

                var response = new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = JsonConvert.SerializeObject(user),
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

        public async Task<APIGatewayProxyResponse> AddUserAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                var user = JsonConvert.DeserializeObject<User>(request?.Body);

                if (string.IsNullOrEmpty(user.email) || string.IsNullOrEmpty(user.password))
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "Missing required parameter",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                var search = this.DDBContext.ScanAsync<User>(null);
                var users = await search.GetNextSetAsync();
                var matchUser = users.FirstOrDefault(e => e.email == user.email);
                if (matchUser != null)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "Email already exists",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                user.user_id = Guid.NewGuid().ToString();
                user.created_timestamp = DateTime.Now;
                user.uploaded_photo_id = new List<string>();
                user.liked_photo_id = new List<string>();
                user.followed_friend_id = new List<string>();
                context.Logger.LogLine($"Saving user with id {user.user_id}");
                await DDBContext.SaveAsync<User>(user);

                var response = new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = JsonConvert.SerializeObject(new { user.user_id }),
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

        public async Task<APIGatewayProxyResponse> SignInAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                var user = JsonConvert.DeserializeObject<User>(request?.Body);

                var search = this.DDBContext.ScanAsync<User>(null);
                var users = await search.GetNextSetAsync();

                var matchUser = users.FirstOrDefault(e => e.email == user.email);
                if (matchUser != null && matchUser.password == user.password)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = 200,
                        Body = JsonConvert.SerializeObject(new { matchUser.user_id }),
                        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                    };
                }

                var response = new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.Unauthorized
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

        public async Task<APIGatewayProxyResponse> RemoveUserAsync(APIGatewayProxyRequest request, ILambdaContext context)
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

                context.Logger.LogLine($"Deleting user with id {userId}");
                await DDBContext.DeleteAsync<User>(userId);

                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK
                };
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
