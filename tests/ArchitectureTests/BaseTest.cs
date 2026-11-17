using System.Reflection;
using CodeBench.Contracts;
using Web.Api;

namespace ArchitectureTests;

public abstract class BaseTest
{
    protected static readonly Assembly WebApiAssembly = typeof(DependencyInjection).Assembly;

    protected static readonly Assembly ContractsAssembly = typeof(Subjects).Assembly;

    protected static readonly Assembly WorkerAssembly = Assembly.Load("CodeBench.Worker");
}
