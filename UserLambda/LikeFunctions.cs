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
    public class LikeFunctions
    {
        const string USER_TABLE_NAME_LOOKUP = "UserTable";
        private const string USER_ID_PATH = "user_id";

        IDynamoDBContext DDBContext { get; }

        public LikeFunctions()
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

        public LikeFunctions(IAmazonDynamoDB ddbClient, string tableName)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(User)] = new Amazon.Util.TypeMapping(typeof(User), tableName);
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(Photo)] = new Amazon.Util.TypeMapping(typeof(Photo), "photoInfo");
            }

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DDBContext = new DynamoDBContext(ddbClient, config);
        }

        public async Task<APIGatewayProxyResponse> LikeAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                string userId = null;
                if (request.PathParameters != null && request.PathParameters.ContainsKey(USER_ID_PATH))
                    userId = request.PathParameters[USER_ID_PATH];

                var requestBody = JsonConvert.DeserializeObject<LikeUnlikeRequest>(request.Body);

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(requestBody.photo_id))
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "Missing required parameter",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                var user = await DDBContext.LoadAsync<User>(userId);
                var photo = await DDBContext.LoadAsync<Photo>(requestBody.photo_id);

                if (user == null)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "User cannot be found",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                if (photo == null)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "Photo cannot be found",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }


                if (user.liked_photo_id.Contains(photo.photo_id))
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "You have already liked this photo",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                user.liked_photo_id.Add(photo.photo_id);

                await DDBContext.SaveAsync<User>(user);

                photo.liked_user_id.Add(user.user_id);

                await DDBContext.SaveAsync<Photo>(photo);

                var response = new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = JsonConvert.SerializeObject(new { requestBody.photo_id }),
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

        public async Task<APIGatewayProxyResponse> UnlikeAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                string userId = null;
                if (request.PathParameters != null && request.PathParameters.ContainsKey(USER_ID_PATH))
                    userId = request.PathParameters[USER_ID_PATH];

                var requestBody = JsonConvert.DeserializeObject<LikeUnlikeRequest>(request.Body);

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(requestBody.photo_id))
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "Missing required parameter",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                var user = await DDBContext.LoadAsync<User>(userId);
                var photo = await DDBContext.LoadAsync<Photo>(requestBody.photo_id);

                if (user == null)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "User cannot be found",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                if (photo == null)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "Photo cannot be found",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }


                if (!user.liked_photo_id.Contains(photo.photo_id))
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Body = "You have not liked this photo",
                        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                    };
                }

                user.liked_photo_id.RemoveAll(e => e == photo.photo_id);

                await DDBContext.SaveAsync<User>(user);

                photo.liked_user_id.RemoveAll(e => e == user.user_id);

                await DDBContext.SaveAsync<Photo>(photo);

                var response = new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = JsonConvert.SerializeObject(new { requestBody.photo_id }),
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
