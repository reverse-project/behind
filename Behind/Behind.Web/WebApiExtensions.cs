using Scalar.AspNetCore;

namespace Behind.Web;

public static class WebApiExtensions
{
    extension(WebApplication application)
    {
        public WebApplication UseWeb()
        {
            application.MapScalarApiReference();
            
            return application;
        }
    }
}