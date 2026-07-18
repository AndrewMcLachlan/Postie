using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Postie.AspNetCore;

namespace Postie.Cqrs.AspNetCore.Tests;

public class EndpointMappingGuardTests
{
    private static IEndpointRouteBuilder BuildApp()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddPostie(typeof(GetGreeting).Assembly);
        return builder.Build();
    }

    public static IEnumerable<object[]> EndpointsNullCases()
    {
        yield return new object[] { "MapQuery", (Action)(() => PostieEndpointRouteBuilderExtensions.MapQuery<GetGreeting, string>(null!, "/x")) };
        yield return new object[] { "MapStreamQuery", (Action)(() => PostieEndpointRouteBuilderExtensions.MapStreamQuery<StreamWidgets, Widget>(null!, "/x")) };
        yield return new object[] { "MapCommand<T,R>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapCommand<SubmitWidget, Widget>(null!, "/x")) };
        yield return new object[] { "MapCommand<T>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapCommand<DeleteWidget>(null!, "/x")) };
        yield return new object[] { "MapPutCommand<T,R>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCommand<SubmitWidget, Widget>(null!, "/x")) };
        yield return new object[] { "MapPutCommand<T>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCommand<DeleteWidget>(null!, "/x")) };
        yield return new object[] { "MapPatchCommand<T,R>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPatchCommand<SubmitWidget, Widget>(null!, "/x")) };
        yield return new object[] { "MapPostCreate (delegating)", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPostCreate<CreateWidget, Widget>(null!, "/x", "route", (Func<Widget, object?>)(r => r))) };
        yield return new object[] { "MapPostCreate (target)", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPostCreate<CreateWidget, Widget>(null!, "/x", "route", (Func<CreateWidget, Widget, object?>)((_, r) => r))) };
        yield return new object[] { "MapPutCreate (delegating)", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCreate<CreateWidget, Widget>(null!, "/x", "route", (Func<Widget, object?>)(r => r))) };
        yield return new object[] { "MapPutCreate (target)", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCreate<CreateWidget, Widget>(null!, "/x", "route", (Func<CreateWidget, Widget, object?>)((_, r) => r))) };
        yield return new object[] { "MapDeleteCommand<T>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapDeleteCommand<DeleteWidget>(null!, "/x")) };
        yield return new object[] { "MapDeleteCommand<T,R>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapDeleteCommand<PurgeWidget, Widget>(null!, "/x")) };
    }

    /// <summary>
    /// Given a null IEndpointRouteBuilder.
    /// When any public Map* extension method is called.
    /// Then an ArgumentNullException naming "endpoints" is thrown at map time.
    /// </summary>
    [Theory]
    [MemberData(nameof(EndpointsNullCases))]
    [Trait("Category", "Unit")]
    public void NullEndpointsThrowsArgumentNullException(string label, Action map)
    {
        var exception = Assert.Throws<ArgumentNullException>(map);

        Assert.Equal("endpoints", exception.ParamName);
        Assert.NotEmpty(label);
    }

    public static IEnumerable<object[]> PatternNullCases()
    {
        yield return new object[] { "MapQuery", (Action)(() => PostieEndpointRouteBuilderExtensions.MapQuery<GetGreeting, string>(BuildApp(), null!)) };
        yield return new object[] { "MapStreamQuery", (Action)(() => PostieEndpointRouteBuilderExtensions.MapStreamQuery<StreamWidgets, Widget>(BuildApp(), null!)) };
        yield return new object[] { "MapCommand<T,R>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapCommand<SubmitWidget, Widget>(BuildApp(), null!)) };
        yield return new object[] { "MapCommand<T>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapCommand<DeleteWidget>(BuildApp(), null!)) };
        yield return new object[] { "MapPutCommand<T,R>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCommand<SubmitWidget, Widget>(BuildApp(), null!)) };
        yield return new object[] { "MapPutCommand<T>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCommand<DeleteWidget>(BuildApp(), null!)) };
        yield return new object[] { "MapPatchCommand<T,R>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPatchCommand<SubmitWidget, Widget>(BuildApp(), null!)) };
        yield return new object[] { "MapPostCreate (delegating)", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPostCreate<CreateWidget, Widget>(BuildApp(), null!, "route", (Func<Widget, object?>)(r => r))) };
        yield return new object[] { "MapPostCreate (target)", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPostCreate<CreateWidget, Widget>(BuildApp(), null!, "route", (Func<CreateWidget, Widget, object?>)((_, r) => r))) };
        yield return new object[] { "MapPutCreate (delegating)", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCreate<CreateWidget, Widget>(BuildApp(), null!, "route", (Func<Widget, object?>)(r => r))) };
        yield return new object[] { "MapPutCreate (target)", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCreate<CreateWidget, Widget>(BuildApp(), null!, "route", (Func<CreateWidget, Widget, object?>)((_, r) => r))) };
        yield return new object[] { "MapDeleteCommand<T>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapDeleteCommand<DeleteWidget>(BuildApp(), null!)) };
        yield return new object[] { "MapDeleteCommand<T,R>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapDeleteCommand<PurgeWidget, Widget>(BuildApp(), null!)) };
    }

    /// <summary>
    /// Given a null route pattern.
    /// When any public Map* extension method is called.
    /// Then an ArgumentNullException naming "pattern" is thrown at map time (an empty pattern is legal,
    /// since it maps the root of a route group, so only null is rejected).
    /// </summary>
    [Theory]
    [MemberData(nameof(PatternNullCases))]
    [Trait("Category", "Unit")]
    public void NullPatternThrowsArgumentNullException(string label, Action map)
    {
        var exception = Assert.Throws<ArgumentNullException>(map);

        Assert.Equal("pattern", exception.ParamName);
        Assert.NotEmpty(label);
    }

    public static IEnumerable<object[]> RouteNameInvalidCases()
    {
        yield return new object[] { "MapPostCreate-null", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPostCreate<CreateWidget, Widget>(BuildApp(), "/x", null!, (Func<CreateWidget, Widget, object?>)((_, r) => r))), typeof(ArgumentNullException) };
        yield return new object[] { "MapPostCreate-empty", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPostCreate<CreateWidget, Widget>(BuildApp(), "/x", "", (Func<CreateWidget, Widget, object?>)((_, r) => r))), typeof(ArgumentException) };
        yield return new object[] { "MapPutCreate-null", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCreate<CreateWidget, Widget>(BuildApp(), "/x", null!, (Func<CreateWidget, Widget, object?>)((_, r) => r))), typeof(ArgumentNullException) };
        yield return new object[] { "MapPutCreate-empty", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCreate<CreateWidget, Widget>(BuildApp(), "/x", "", (Func<CreateWidget, Widget, object?>)((_, r) => r))), typeof(ArgumentException) };
    }

    /// <summary>
    /// Given a null or empty routeName.
    /// When MapPostCreate or MapPutCreate is called.
    /// Then null throws ArgumentNullException and empty throws ArgumentException, both naming "routeName"
    /// (an empty route name can never match a named route, so unlike pattern, empty is invalid here).
    /// </summary>
    [Theory]
    [MemberData(nameof(RouteNameInvalidCases))]
    [Trait("Category", "Unit")]
    public void InvalidRouteNameThrows(string label, Action map, Type expectedExceptionType)
    {
        var exception = Assert.Throws(expectedExceptionType, map);

        Assert.Equal("routeName", ((ArgumentException)exception).ParamName);
        Assert.NotEmpty(label);
    }

    public static IEnumerable<object[]> GetRouteValuesNullCases()
    {
        yield return new object[] { "MapPostCreate-target", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPostCreate<CreateWidget, Widget>(BuildApp(), "/x", "route", (Func<CreateWidget, Widget, object?>)null!)) };
        yield return new object[] { "MapPostCreate-delegating", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPostCreate<CreateWidget, Widget>(BuildApp(), "/x", "route", (Func<Widget, object?>)null!)) };
        yield return new object[] { "MapPutCreate-target", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCreate<CreateWidget, Widget>(BuildApp(), "/x", "route", (Func<CreateWidget, Widget, object?>)null!)) };
        yield return new object[] { "MapPutCreate-delegating", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCreate<CreateWidget, Widget>(BuildApp(), "/x", "route", (Func<Widget, object?>)null!)) };
    }

    /// <summary>
    /// Given a null getRouteValues delegate.
    /// When MapPostCreate or MapPutCreate is called, on either overload.
    /// Then an ArgumentNullException naming "getRouteValues" is thrown at map time, not deferred to
    /// inside the per-request wrapper lambda that the response-only overload builds.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetRouteValuesNullCases))]
    [Trait("Category", "Unit")]
    public void NullGetRouteValuesThrowsAtMapTime(string label, Action map)
    {
        var exception = Assert.Throws<ArgumentNullException>(map);

        Assert.Equal("getRouteValues", exception.ParamName);
        Assert.NotEmpty(label);
    }

    public static IEnumerable<object[]> UndefinedBindingCases()
    {
        const RequestBinding undefined = (RequestBinding)42;

        yield return new object[] { "MapCommand<T,R>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapCommand<SubmitWidget, Widget>(BuildApp(), "/x", binding: undefined)) };
        yield return new object[] { "MapCommand<T>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapCommand<DeleteWidget>(BuildApp(), "/x", binding: undefined)) };
        yield return new object[] { "MapPutCommand<T,R>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCommand<SubmitWidget, Widget>(BuildApp(), "/x", binding: undefined)) };
        yield return new object[] { "MapPutCommand<T>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCommand<DeleteWidget>(BuildApp(), "/x", binding: undefined)) };
        yield return new object[] { "MapPatchCommand<T,R>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPatchCommand<SubmitWidget, Widget>(BuildApp(), "/x", binding: undefined)) };
        yield return new object[] { "MapPostCreate", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPostCreate<CreateWidget, Widget>(BuildApp(), "/x", "route", (Func<CreateWidget, Widget, object?>)((_, r) => r), undefined)) };
        yield return new object[] { "MapPutCreate", (Action)(() => PostieEndpointRouteBuilderExtensions.MapPutCreate<CreateWidget, Widget>(BuildApp(), "/x", "route", (Func<CreateWidget, Widget, object?>)((_, r) => r), undefined)) };
        yield return new object[] { "MapDeleteCommand<T>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapDeleteCommand<DeleteWidget>(BuildApp(), "/x", undefined)) };
        yield return new object[] { "MapDeleteCommand<T,R>", (Action)(() => PostieEndpointRouteBuilderExtensions.MapDeleteCommand<PurgeWidget, Widget>(BuildApp(), "/x", binding: undefined)) };
    }

    /// <summary>
    /// Given an undefined RequestBinding value, e.g. (RequestBinding)42.
    /// When any binding-taking Map* extension method is called.
    /// Then an ArgumentOutOfRangeException naming "binding" is thrown at map time, instead of the
    /// undefined value silently falling through to Default binding at request time.
    /// </summary>
    [Theory]
    [MemberData(nameof(UndefinedBindingCases))]
    [Trait("Category", "Unit")]
    public void UndefinedBindingThrowsArgumentOutOfRangeException(string label, Action map)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(map);

        Assert.Equal("binding", exception.ParamName);
        Assert.NotEmpty(label);
    }
}
