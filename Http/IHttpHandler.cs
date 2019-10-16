using System.Threading.Tasks;

namespace Rpi.Http
{
    /// <summary>
    /// Defines an IHttpHandler to map to a path.
    /// </summary>
    public interface IHttpHandler
    {
        void PreInitialize();
        void PostInitialize();
        Task ProcessRequest(SimpleHttpContext context);
    }
}
