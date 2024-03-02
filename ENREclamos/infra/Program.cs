using System.Threading.Tasks;
using Pulumi;

namespace ENREclamos.Infrastructure;

class Program
{
    static Task<int> Main() => Deployment.RunAsync<ENREStack>();
}