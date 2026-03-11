using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Uploads;
using FluentValidation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace DM.Infrastructure.Core.Storage;

/// <inheritdoc />
internal class PublicImageService : IPublicImageService
{
    private readonly IValidator<CreateUpload> validator;
    private readonly INameGenerator nameGenerator;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IUploader uploader;
    private readonly IUploadFactory factory;
    private readonly IPublicImageUploadRepository repository;
    private readonly IIdentityProvider identityProvider;

    /// <inheritdoc />
    public PublicImageService(
        IValidator<CreateUpload> validator,
        INameGenerator nameGenerator,
        IDateTimeProvider dateTimeProvider,
        IUploader uploader,
        IUploadFactory factory,
        IPublicImageUploadRepository repository,
        IIdentityProvider identityProvider)
    {
        this.validator = validator;
        this.nameGenerator = nameGenerator;
        this.dateTimeProvider = dateTimeProvider;
        this.uploader = uploader;
        this.factory = factory;
        this.repository = repository;
        this.identityProvider = identityProvider;
    }

    private static readonly Size MediumSize = new(200, 200);
    private static readonly Size SmallSize = new(100, 100);

    /// <inheritdoc />
    public async Task<(Upload original, Upload medium, Upload small)> UploadAsync(CreateUpload createUpload)
    {
        await validator.ValidateAndThrowAsync(createUpload).ConfigureAwait(false);
        var (name, extension) = await nameGenerator.Generate(createUpload).ConfigureAwait(false);

        using var image = await Image.LoadAsync(createUpload.StreamAccessor()).ConfigureAwait(false);
        var cropRectangle = image.Height > image.Width
            ? new Rectangle(0, (image.Height - image.Width) / 2, image.Width, image.Width)
            : new Rectangle((image.Width - image.Height) / 2, 0, image.Height, image.Height);

        await using var mediumImageStream = new MemoryStream();
        await image.Clone(c => c.Crop(cropRectangle).Resize(MediumSize)).SaveAsJpegAsync(mediumImageStream).ConfigureAwait(false);

        await using var smallImageStream = new MemoryStream();
        await image.Clone(c => c.Crop(cropRectangle).Resize(SmallSize)).SaveAsJpegAsync(smallImageStream).ConfigureAwait(false);

        var originalImagePath = await uploader.Upload(createUpload.StreamAccessor, $"{name}{extension}").ConfigureAwait(false);
        var mediumImagePath = await uploader.Upload(() => mediumImageStream, $"{name}_m.jpg").ConfigureAwait(false);
        var smallImagePath = await uploader.Upload(() => smallImageStream, $"{name}_s.jpg").ConfigureAwait(false);

        var userId = identityProvider.Current.User.UserId;
        var createdAt = dateTimeProvider.Now;
        var uploads = new[] {originalImagePath, mediumImagePath, smallImagePath}
            .Select(path => factory.Create(createUpload, path, userId, path == originalImagePath, createdAt));

        var uploadsIndex = (await repository.CreateAsync(uploads).ConfigureAwait(false)).ToDictionary(u => u.FilePath);
        return (uploadsIndex[originalImagePath], uploadsIndex[mediumImagePath], uploadsIndex[smallImagePath]);
    }

    /// <inheritdoc />
    public Task PrepareObsoleteForDeletingAsync(Guid entityId) => repository.RemoveObsoleteUploadsAsync(entityId);
}
