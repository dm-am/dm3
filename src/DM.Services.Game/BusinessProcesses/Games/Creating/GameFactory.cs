using System;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.Game.Dto.Input;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;

namespace DM.Services.Game.BusinessProcesses.Games.Creating;

/// <inheritdoc />
internal class GameFactory : IGameFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public GameFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public DbGame Create(CreateGame createGame, Guid masterId, ModuleStatus initialStatus,
        PremoderationStatus premoderationStatus, bool isRecruitmentOpen)
    {
        return new DbGame
        {
            GameId = _guidFactory.Create(),
            CreatedUtc = _dateTimeProvider.Now,
            Status = initialStatus,
            PremoderationStatus = premoderationStatus,
            IsRecruitmentOpen = isRecruitmentOpen,
            RecruitmentStartedUtc = isRecruitmentOpen ? _dateTimeProvider.Now : null,
            MasterId = masterId,
            AssistantId = null,
            Title = createGame.Title,
            SystemName = createGame.SystemName,
            NarrativeSetting = createGame.NarrativeSetting,
            AttributeSchemaId = createGame.AttributeSchemaId,
            Info = createGame.Info,
            DisableAlignment = createGame.DisableAlignment,
            HideTemper = createGame.HideTemper,
            HideStory = createGame.HideStory,
            HideSkills = createGame.HideSkills,
            HideInventory = createGame.HideInventory,
            HideDiceResult = createGame.HideDiceResult,
            ShowPrivateMessages = createGame.ShowPrivateMessages,
            HidePostStats = createGame.HidePostStats,
            CommentariesAccessMode = createGame.CommentariesAccessMode,
            Notepad = ""
        };
    }
}