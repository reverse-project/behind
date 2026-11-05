using Behind.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class CarouselResponseExtensions
{
    public const string VersionConflictReason = "version-conflict";

    extension(Guid imageId)
    {
        public string ToCarouselImageUrl() => $"{VfsTypes.CarouselImageUrl}/{imageId}";
    }

    extension(CarouselsConfiguration configuration)
    {
        public CarouselSettingsResponse ToResponse() =>
            new(
                configuration.SlideLimit,
                configuration.IntervalRange.Min,
                configuration.IntervalRange.Max,
                configuration.DefaultInterval,
                configuration.CaptionLimit,
                CarouselSlide.MaxLinkUrlLength,
                configuration.ImageLimit);
    }

    extension(ICarouselQueries.CarouselDetail carousel)
    {
        public CarouselResponse ToResponse() =>
            new(
                carousel.Id.Key,
                carousel.Id.Version,
                carousel.IntervalSeconds,
                carousel.Slides.Select(slide => slide.ToItem()).ToList());
    }

    extension(ICarouselQueries.CarouselSlideInfo slide)
    {
        private CarouselSlideItem ToItem() =>
            new(
                slide.Id,
                slide.ImageId,
                slide.ImageId.ToCarouselImageUrl(),
                slide.Caption,
                slide.LinkUrl.IsSafeCarouselLink ? slide.LinkUrl : string.Empty,
                slide.Position);
    }

    extension(CarouselSaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                CarouselSaveResult.Saved saved => Results.Json(new CarouselSavedResponse(saved.Id.Key, saved.Id.Version)),
                CarouselSaveResult.NotFound => Results.NotFound(),
                CarouselSaveResult.UnknownImage => Results.BadRequest(),
                _ => Results.Conflict(new CarouselConflictResponse(VersionConflictReason))
            };
    }
}
