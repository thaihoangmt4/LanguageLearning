using System.Reflection;
using LanguageLearning.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace LanguageLearning.WebApi.Configuration;

public sealed class DevelopmentControllerFeatureProvider(IHostEnvironment environment) : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo) =>
        base.IsController(typeInfo) &&
        IsAvailableInEnvironment(typeInfo.AsType(), environment);

    public static bool IsAvailableInEnvironment(Type controllerType, IHostEnvironment hostEnvironment) =>
        hostEnvironment.IsDevelopment() || controllerType != typeof(TestLearningProgressController);
}
