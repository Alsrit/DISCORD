using FluentValidation;
using Platform.Application.Models;

namespace Platform.Application.Validators;

public sealed class UploadedLocalizationFileDtoValidator : AbstractValidator<UploadedLocalizationFileDto>
{
    public UploadedLocalizationFileDtoValidator()
    {
        RuleFor(x => x.RelativePath).NotEmpty().MaximumLength(260);
        RuleFor(x => x.Content).NotEmpty();
        RuleFor(x => x.SourceLanguage).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Sha256).NotEmpty().MaximumLength(128);
        RuleFor(x => x.SizeBytes).GreaterThan(0);
    }
}

public sealed class AnalyzeModRequestValidator : AbstractValidator<AnalyzeModRequest>
{
    public AnalyzeModRequestValidator()
    {
        RuleFor(x => x.ModName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.ModVersion).MaximumLength(64);
        RuleFor(x => x.OriginalModReference).MaximumLength(260);
        RuleFor(x => x.SourceLanguage).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Files).NotEmpty().Must(x => x.Count <= 128)
            .WithMessage("Количество файлов не должно превышать 128.");
        RuleForEach(x => x.Files).SetValidator(new UploadedLocalizationFileDtoValidator());
    }
}

public sealed class CreateTranslationJobRequestValidator : AbstractValidator<CreateTranslationJobRequest>
{
    public CreateTranslationJobRequestValidator()
    {
        RuleFor(x => x.ModName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.OriginalModReference).MaximumLength(260);
        RuleFor(x => x.SourceLanguage).NotEmpty().MaximumLength(16);
        RuleFor(x => x.TargetLanguage).NotEmpty().MaximumLength(16);
        RuleFor(x => x.RequestedSubmodName).MaximumLength(160);
        RuleFor(x => x.ProviderCode).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Files).NotEmpty().Must(x => x.Count <= 128)
            .WithMessage("Количество файлов не должно превышать 128.");
        RuleForEach(x => x.Files).SetValidator(new UploadedLocalizationFileDtoValidator());
    }
}
