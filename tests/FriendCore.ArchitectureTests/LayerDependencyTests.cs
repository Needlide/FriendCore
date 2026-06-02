using System.Reflection;
using FriendCore.API.Extensions;
using FriendCore.Application;
using FriendCore.Domain.Common;
using MediatR;
using NetArchTest.Rules;
using DependencyInjection = FriendCore.Infrastructure.DependencyInjection;

namespace FriendCore.ArchitectureTests;

/// <summary>
///     Enforces Clean Architecture dependency rules from the technical spec (§2.2).
///     Run with: dotnet test tests/FriendCore.ArchitectureTests
/// </summary>
public class LayerDependencyTests
{
    private const string DomainNamespace = "FriendCore.Domain";
    private const string ApplicationNamespace = "FriendCore.Application";
    private const string InfrastructureNamespace = "FriendCore.Infrastructure";
    private const string ApiNamespace = "FriendCore.API";
    private static readonly Assembly DomainAssembly = typeof(Entity<>).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(AssemblyReference).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;
    private static readonly Assembly ApiAssembly = typeof(ErrorOrExtensions).Assembly;

    [Fact]
    public void Domain_Should_Not_Depend_On_Other_Layers()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        AssertArchitectureRule(result);
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        AssertArchitectureRule(result);
    }

    [Fact]
    public void Application_Should_Not_Reference_Infrastructure_Technologies()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "StackExchange.Redis",
                "Hangfire",
                "Microsoft.AspNetCore")
            .GetResult();

        AssertArchitectureRule(result);
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        AssertArchitectureRule(result);
    }

    [Fact]
    public void Api_Should_Not_Depend_On_Domain()
    {
        // API may reference Infrastructure for composition root (AddInfrastructure) only — not Domain types.
        var result = Types.InAssembly(ApiAssembly)
            .ShouldNot()
            .HaveDependencyOn(DomainNamespace)
            .GetResult();

        AssertArchitectureRule(result);
    }

    [Fact]
    public void Api_Should_Not_Contain_MediatR_Handlers()
    {
        var result = Types.InAssembly(ApiAssembly)
            .ShouldNot()
            .ImplementInterface(typeof(IRequestHandler<,>))
            .GetResult();

        AssertArchitectureRule(result);
    }

    private static void AssertArchitectureRule(TestResult result)
    {
        var failing = result.FailingTypes ?? [];
        Assert.True(
            result.IsSuccessful,
            $"Architecture rule failed. Violating types:{Environment.NewLine}{string.Join(Environment.NewLine, failing.Select(t => t.FullName))}");
    }
}