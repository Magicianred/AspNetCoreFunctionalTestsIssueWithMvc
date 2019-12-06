using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace RequestMessageIssueTests
{
    public sealed class Program
    {
        public static void Main(string[] args) => CreateWebHostBuilder(args).Build().Run();

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }

    public class Comment
    {
        [Required]
        public string Author { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        [HttpPost]
        public Comment Post([FromBody] Comment value) => value;
    }

    [Route("api/[controller]")]
    [ApiController]
    public class IssuesController : ControllerBase
    {
    }

    public class RequestMessagesTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;

        public RequestMessagesTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GivenAnController_AnAction_And_AValidRequest_WhenAssertionTheRequestContent_ItShouldHaveAvailableTheContent()
        {
            using var client = _factory.CreateClient();

            var response = await client.PostAsync("/api/comments", new StringContent(@"{
                      ""author"": ""John""
                    }", Encoding.UTF8, "application/json"));

            // as expected
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // fails, the HttpContent from the originated request doesn't contain the request body
            var requestContent = await response.RequestMessage.Content.ReadAsStringAsync();

            Assert.NotEmpty(requestContent);
        }

        [Fact]
        public async Task Given_ARequest_WithoutACorrespondingMVCAction_WhenAssertionTheRequestContent_ItShouldHaveAvailableTheContent()
        {
            using var client = _factory.CreateClient();

            var response = await client.PostAsync("/api/issues", new StringContent(@"{
                      ""issue"": ""An issue""
                    }", Encoding.UTF8, "application/json"));

            // as expected
            Assert.False(response.IsSuccessStatusCode);

            // fails, the HttpContent from the originated request doesn't contain the request body
            var requestContent = await response.RequestMessage.Content.ReadAsStringAsync();

            Assert.NotEmpty(requestContent);
        }

        [Fact]
        public async Task GivenAnController_AnAction_And_ABadRequestRequest_WhenAssertionTheRequestContent_ItShouldHaveAvailableTheContent()
        {
            using var client = _factory.CreateClient();

            var response = await client.PostAsync("/api/comments", new StringContent(@"{
                      ""comment"": ""some comment""
                    }", Encoding.UTF8, "application/json"));

            // as expected
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            // fails, the HttpContent from the originated request doesn't contain the request body
            var requestContent = await response.RequestMessage.Content.ReadAsStringAsync();
            Assert.NotEmpty(requestContent);
        }

        [Fact]
        public async Task GivenAnWebHostBuilderSetup_WhenRequestHasContent_ShouldPrintContent()
        {
            var builder = new WebHostBuilder();
            builder.ConfigureServices(services =>
            {
                services.AddRouting();
            });
            builder.Configure(app => app.UseRouting());
            using var testServer = new TestServer(builder);
            using var client = testServer.CreateClient();

            using var response = await client.PostAsync("/endpoint", new StringContent("request body"));

            // expected
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            // as expected, the request content is not empty, it is "request body"
            var requestContent = await response.RequestMessage.Content.ReadAsStringAsync();
            Assert.NotEmpty(requestContent);
        }
    }
}
