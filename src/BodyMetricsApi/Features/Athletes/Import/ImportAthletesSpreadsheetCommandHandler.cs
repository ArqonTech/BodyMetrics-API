using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BodyMetricsApi.Features.Athletes.Import.Dtos;
using BodyMetricsApi.Features.Athletes.Import.ViewModels;
using BodyMetricsApi.Features.Athletes.PhysicalAssessments;
using BodyMetricsApi.Features.Athletes.PhysicalAssessments.Shared.ValueObjects;
using BodyMetricsApi.Features.Athletes.Shared.Enums;
using BodyMetricsApi.Features.Athletes.Shared.Interfaces;
using BodyMetricsApi.Features.AthleteGroups;
using BodyMetricsApi.Features.AthleteGroups.Shared.Interfaces;
using BodyMetricsApi.Features.Sports;
using BodyMetricsApi.Features.Sports.Shared.Interfaces;
using BodyMetricsApi.Shared.Authentication;
using BodyMetricsApi.Shared.Results;
using BodyMetricsApi.Shared.Validation;
using ClosedXML.Excel;
using FluentValidation;

namespace BodyMetricsApi.Features.Athletes.Import;

public sealed class ImportAthletesSpreadsheetCommandHandler(
    IAthleteRepository athleteRepository,
    IAthleteGroupRepository groupRepository,
    ISportRepository sportRepository,
    ICurrentUserService currentUserService,
    IValidator<ImportAthletesSpreadsheetCommand> validator)
{
    private const string SectorColumn = "Setor";
    private const string PositionColumn = "Posição";
    private const string PhaseColumn = "Fase";
    private const string FullNameColumn = "Nome";
    private const string SexColumn = "Sexo";
    private const string EthnicityColumn = "Raça";
    private const string CategoryColumn = "Categoria";
    private const string BirthDateColumn = "Nascimento";
    private const string AssessmentDateColumn = "Data avaliação";
    private const string WeightColumn = "Peso";
    private const string HeightColumn = "Altura";
    private const string SittingHeightColumn = "Altura sentado";
    private const string RightTricepsColumn = "Tricep D.";
    private const string LeftTricepsColumn = "Tricep E.";
    private const string SubscapularColumn = "Sub esc";
    private const string ThoraxColumn = "Torax";
    private const string SubaxillaryColumn = "Sub. Axi";
    private const string IliacCrestColumn = "Crist. ilíaca";
    private const string SupraspinaleColumn = "Sup. Espin";
    private const string AbdominalColumn = "abd";
    private const string RightThighColumn = "Coxa D";
    private const string LeftThighColumn = "Coxa E";
    private const string RightCalfSkinfoldColumn = "Pantu D";
    private const string LeftCalfSkinfoldColumn = "Pantu E";
    private const string ShoulderColumn = "C. ombro";
    private const string ChestColumn = "C.Peitoral";
    private const string RightArmColumn = "C.Braço D.";
    private const string LeftArmColumn = "C.Braço E.";
    private const string WaistColumn = "C.Cintura";
    private const string AbdominalCircumferenceColumn = "C.Abdominal";
    private const string HipColumn = "C.Quadril";
    private const string RightMidThighColumn = "C. Medial D";
    private const string LeftMidThighColumn = "C.Medial E";
    private const string RightCalfCircumferenceColumn = "Pantu. D.";
    private const string LeftCalfCircumferenceColumn = "Pantu. E.";
    private const string RightWristColumn = "D.Punho";
    private const string RightKneeColumn = "D.Joelho";
    private const string RightAnkleColumn = "D.Tornozelo";
    private const string TeamColumn = "Time";

    // Team is opt-in: older spreadsheets without this column, or rows that leave it blank,
    // import exactly as before (athlete stays standalone).
    // "Supra. lli" was renamed to "Crist. ilíaca" (old header kept as an alias); both new skinfold columns are optional so older files still import.
    private static readonly HashSet<string> OptionalColumns =
        new(StringComparer.Ordinal)
        {
            TeamColumn, AbdominalCircumferenceColumn, IliacCrestColumn, SupraspinaleColumn
        };

    private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");

    // ClosedXML synthesizes "Column<N>" as the text of an unnamed Excel Table header cell.
    // Spreadsheets sometimes carry stray/duplicate Table objects (e.g. from copy-pasting a
    // table-formatted range), which turns an otherwise ordinary data row into that table's
    // header row - any blank cell on it then reads back as this placeholder instead of empty.
    private static readonly Regex SyntheticTableColumnNamePattern = new(@"^Column\d+$", RegexOptions.Compiled);

    private static readonly Dictionary<string, string[]> ColumnAliases = new(StringComparer.Ordinal)
    {
        [SectorColumn] = ["Setor"],
        [PositionColumn] = ["Posição", "Posicao"],
        [PhaseColumn] = ["Fase"],
        [FullNameColumn] = ["Nome"],
        [SexColumn] = ["Sexo"],
        [EthnicityColumn] = ["Raça", "Raca"],
        [CategoryColumn] = ["Categoria"],
        [BirthDateColumn] = ["Nascimento"],
        [AssessmentDateColumn] = ["Data avaliação", "Data avaliacao", "Data-avaliação", "Data-avaliacao"],
        [WeightColumn] = ["Peso"],
        [HeightColumn] = ["Altura"],
        [SittingHeightColumn] = ["Altura sentado"],
        [RightTricepsColumn] = ["Tricep D.", "Triceps D.", "Tríceps D.", "Tricep D", "Triceps D", "Tríceps D"],
        [LeftTricepsColumn] = ["Tricep E.", "Triceps E.", "Tríceps E.", "Tricep E", "Triceps E", "Tríceps E"],
        [SubscapularColumn] = ["Sub esc", "Sub. esc"],
        [ThoraxColumn] = ["Torax", "Tórax"],
        [SubaxillaryColumn] = ["Sub. Axi", "Sub Axi"],
        [IliacCrestColumn] = ["Crist. ilíaca", "Crist ilíaca", "Crista ilíaca", "Crist. ilíaca.", "Cr. ilíaca", "Supra. lli", "Supra lli", "Supra. Ili", "Supra Ili"],
        [SupraspinaleColumn] = ["Sup. Espin", "Sup Espin", "Sup. Espin.", "Supraespinal", "Supra espinal"],
        [AbdominalColumn] = ["abd", "abd.", "Abdominal"],
        [RightThighColumn] = ["Coxa D", "Coxa D."],
        [LeftThighColumn] = ["Coxa E", "Coxa E."],
        [RightCalfSkinfoldColumn] = ["Pantu D", "Pantu D."],
        [LeftCalfSkinfoldColumn] = ["Pantu E", "Pantu E."],
        [ShoulderColumn] = ["C. ombro", "C.Ombro", "COmbro", "C ombro"],
        [ChestColumn] = ["C.Peitoral", "C. Peitoral", "C Peitoral"],
        [RightArmColumn] = ["C.Braço D.", "C.Braco D.", "C. Braço D.", "C. Braco D.", "C Braço D", "C Braco D"],
        [LeftArmColumn] = ["C.Braço E.", "C.Braco E.", "C. Braço E.", "C. Braco E.", "C Braço E", "C Braco E"],
        [WaistColumn] = ["C.Cintura", "C. Cintura", "C Cintura"],
        [AbdominalCircumferenceColumn] = ["C.Abdominal", "C. Abdominal", "C Abdominal"],
        [HipColumn] = ["C.Quadril", "C. Quadril", "C Quadril"],
        [RightMidThighColumn] = ["C. Medial D", "C.Medial D", "C Medial D"],
        [LeftMidThighColumn] = ["C.Medial E", "C. Medial E", "C Medial E", "C. Medial E."],
        [RightCalfCircumferenceColumn] = ["Pantu. D.", "Pantu D.", "Pantu.D.", "Pantu D"],
        [LeftCalfCircumferenceColumn] = ["Pantu. E.", "Pantu E.", "Pantu.E.", "Pantu E"],
        [RightWristColumn] = ["D.Punho", "D. Punho", "D Punho"],
        [RightKneeColumn] = ["D.Joelho", "D. Joelho", "D Joelho"],
        [RightAnkleColumn] = ["D.Tornozelo", "D. Tornozelo", "D Tornozelo"],
        [TeamColumn] = ["Time"]
    };

    public async Task<OperationResult<AthleteSpreadsheetImportViewModel>> HandleAsync(
        ImportAthletesSpreadsheetCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return OperationResult<AthleteSpreadsheetImportViewModel>.Validation(validationResult.ToErrorDictionary());
        }

        List<ImportAthleteSpreadsheetRowDto> importedRows;
        try
        {
            await using var stream = command.File!.OpenReadStream();
            importedRows = ReadRows(stream);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<AthleteSpreadsheetImportViewModel>.Validation(new Dictionary<string, string[]>
            {
                [nameof(command.File)] = [exception.Message]
            });
        }
        catch (Exception)
        {
            return OperationResult<AthleteSpreadsheetImportViewModel>.Validation(new Dictionary<string, string[]>
            {
                [nameof(command.File)] = ["The uploaded file could not be read as a valid .xlsx spreadsheet."]
            });
        }

        if (importedRows.Count == 0)
        {
            return OperationResult<AthleteSpreadsheetImportViewModel>.Validation(new Dictionary<string, string[]>
            {
                [nameof(command.File)] = ["The spreadsheet does not contain any athlete rows."]
            });
        }

        var distinctSectors = importedRows
            .Select(row => row.Sector)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var distinctCategories = importedRows
            .Select(row => row.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sport = await sportRepository.GetByNameAsync(command.SportName, cancellationToken);
        var sportCreated = false;
        var addedSportSectors = 0;
        var addedSportCategories = 0;

        if (sport is null)
        {
            sport = Sport.Create(command.SportName, distinctSectors, distinctCategories);
            await sportRepository.AddAsync(sport, cancellationToken);
            sportCreated = true;
            addedSportSectors = distinctSectors.Count;
            addedSportCategories = distinctCategories.Count;
        }
        else
        {
            addedSportSectors = distinctSectors.Count(value => !sport.SupportsSector(value));
            addedSportCategories = distinctCategories.Count(value => !sport.SupportsCategory(value));

            if (addedSportSectors > 0 || addedSportCategories > 0)
            {
                sport.MergeOptions(distinctSectors, distinctCategories);
                await sportRepository.ReplaceAsync(sport, cancellationToken);
            }
        }

        var createdAthletes = 0;
        var updatedAthletes = 0;
        var importedAssessments = 0;
        var replacedAssessments = 0;
        var createdGroups = 0;

        var rowsByAthleteName = importedRows.GroupBy(row => row.FullName, StringComparer.OrdinalIgnoreCase).ToList();
        var existingAthletesByName = await athleteRepository.GetByFullNamesAsync(
            currentUserService.UserId, rowsByAthleteName.Select(group => group.Key).ToList(), cancellationToken);

        // An athlete already embedded in a group must still be recognized by name during
        // import - otherwise re-importing the same spreadsheet would create a standalone
        // duplicate instead of updating the athlete in place.
        var ownerGroups = await groupRepository.GetAllByOwnerAsync(currentUserService.UserId, cancellationToken);
        var groupsByName = ownerGroups.ToDictionary(g => NormalizeAthleteName(g.Name), g => g);
        var groupedAthletesByName = new Dictionary<string, AthleteGroup>(StringComparer.Ordinal);
        foreach (var group in ownerGroups)
        {
            foreach (var member in group.Members)
            {
                groupedAthletesByName[NormalizeAthleteName(member.FullName)] = group;
            }
        }

        // Batches writes so a large spreadsheet does one SaveChanges per batch instead of
        // one per athlete, while keeping the pending in-memory set bounded. Only covers the
        // common case (no team column involved) - team-bearing rows write through the group
        // repository directly since embedded-document writes aren't bulk-batchable the same way.
        const int WriteBatchSize = 200;
        var athletesToCreate = new List<Athlete>();
        var athletesToReplace = new List<Athlete>();

        async Task FlushPendingWritesAsync()
        {
            if (athletesToCreate.Count > 0)
            {
                await athleteRepository.AddRangeAsync(athletesToCreate, cancellationToken);
                athletesToCreate.Clear();
            }

            if (athletesToReplace.Count > 0)
            {
                await athleteRepository.ReplaceRangeAsync(athletesToReplace, cancellationToken);
                athletesToReplace.Clear();
            }
        }

        foreach (var rowGroup in rowsByAthleteName)
        {
            var latestRow = rowGroup.Last();
            var groupedAssessments = rowGroup
                .GroupBy(row => row.PhysicalAssessment.AssessmentDate)
                .Select(group => group.Last().PhysicalAssessment)
                .OrderBy(assessment => assessment.AssessmentDate)
                .ToList();

            importedAssessments += groupedAssessments.Count;

            var normalizedName = NormalizeAthleteName(latestRow.FullName);
            existingAthletesByName.TryGetValue(normalizedName, out var standaloneAthlete);
            groupedAthletesByName.TryGetValue(normalizedName, out var currentGroup);
            var athlete = standaloneAthlete ?? currentGroup?.Members.FirstOrDefault(m => NormalizeAthleteName(m.FullName) == normalizedName);

            if (athlete is null)
            {
                athlete = Athlete.Create(
                    currentUserService.UserId,
                    latestRow.FullName,
                    sport,
                    latestRow.Sector,
                    latestRow.Phase,
                    latestRow.Category,
                    latestRow.Sex,
                    latestRow.Ethnicity,
                    latestRow.BirthDate,
                    groupedAssessments,
                    null);

                createdAthletes++;
            }
            else
            {
                var importedDates = groupedAssessments
                    .Select(assessment => assessment.AssessmentDate)
                    .ToHashSet();

                replacedAssessments +=
                    athlete.PhysicalAssessments.Count(assessment => importedDates.Contains(assessment.AssessmentDate));

                var mergedAssessments = athlete.PhysicalAssessments
                    .Where(assessment => !importedDates.Contains(assessment.AssessmentDate))
                    .Concat(groupedAssessments)
                    .OrderBy(assessment => assessment.AssessmentDate)
                    .ToList();

                athlete.Update(
                    latestRow.FullName,
                    sport,
                    latestRow.Sector,
                    latestRow.Phase,
                    latestRow.Category,
                    latestRow.Sex,
                    latestRow.Ethnicity,
                    latestRow.BirthDate,
                    mergedAssessments,
                    athlete.ProfilePhoto);

                updatedAthletes++;
            }

            AthleteGroup? targetGroup = null;
            if (!string.IsNullOrWhiteSpace(latestRow.Team))
            {
                var normalizedTeam = NormalizeAthleteName(latestRow.Team);
                if (!groupsByName.TryGetValue(normalizedTeam, out targetGroup))
                {
                    targetGroup = AthleteGroup.Create(currentUserService.UserId, latestRow.Team.Trim());
                    await groupRepository.AddAsync(targetGroup, cancellationToken);
                    groupsByName[normalizedTeam] = targetGroup;
                    createdGroups++;
                }
            }

            if (targetGroup is not null)
            {
                if (currentGroup is not null && currentGroup.Id != targetGroup.Id)
                {
                    currentGroup.RemoveMember(athlete.Id);
                    await groupRepository.UpdateAsync(currentGroup, cancellationToken);
                }
                else if (currentGroup is null && standaloneAthlete is not null)
                {
                    await athleteRepository.DeleteAsync(athlete.Id, currentUserService.UserId, cancellationToken);
                }

                targetGroup.AddMember(athlete);
                await groupRepository.UpdateAsync(targetGroup, cancellationToken);
                groupedAthletesByName[normalizedName] = targetGroup;
            }
            else if (currentGroup is not null)
            {
                // No team on this row - athlete stays in its current group, just with
                // refreshed profile/assessment data (already applied in place above).
                await groupRepository.UpdateAsync(currentGroup, cancellationToken);
            }
            else
            {
                if (standaloneAthlete is null)
                {
                    athletesToCreate.Add(athlete);
                }
                else
                {
                    athletesToReplace.Add(athlete);
                }

                if (athletesToCreate.Count + athletesToReplace.Count >= WriteBatchSize)
                {
                    await FlushPendingWritesAsync();
                }
            }
        }

        await FlushPendingWritesAsync();

        return OperationResult<AthleteSpreadsheetImportViewModel>.Success(
            new AthleteSpreadsheetImportViewModel(
                sport.Id,
                sport.Name,
                importedRows.Count,
                createdAthletes,
                updatedAthletes,
                importedAssessments,
                replacedAssessments,
                addedSportSectors,
                addedSportCategories,
                sportCreated,
                createdGroups));
    }

    private static string NormalizeAthleteName(string value) => value.Trim().ToUpperInvariant();

    private static List<ImportAthleteSpreadsheetRowDto> ReadRows(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault() ??
                        throw new ArgumentException("The spreadsheet must contain at least one worksheet.");
        var headerRow = worksheet.FirstRowUsed() ??
                        throw new ArgumentException("The spreadsheet must contain a header row.");
        var lastColumnNumber = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

        if (lastColumnNumber == 0)
        {
            throw new ArgumentException("The spreadsheet must contain a header row.");
        }

        var normalizedHeaders = BuildNormalizedHeaderMap(headerRow, lastColumnNumber);
        var columnIndexes = ResolveColumnIndexes(normalizedHeaders);
        var lastRowNumber = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var rows = new List<ImportAthleteSpreadsheetRowDto>();

        for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRowNumber; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (IsRowEmpty(row, columnIndexes))
            {
                continue;
            }

            rows.Add(ReadRow(row, rowNumber, columnIndexes));
        }

        return rows;
    }

    private static Dictionary<string, int> BuildNormalizedHeaderMap(IXLRow headerRow, int lastColumnNumber)
    {
        var normalizedHeaders = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var columnNumber = 1; columnNumber <= lastColumnNumber; columnNumber++)
        {
            var normalizedHeader = NormalizeHeader(headerRow.Cell(columnNumber).GetString());
            if (string.IsNullOrEmpty(normalizedHeader) || normalizedHeaders.ContainsKey(normalizedHeader))
            {
                continue;
            }

            normalizedHeaders[normalizedHeader] = columnNumber;
        }

        return normalizedHeaders;
    }

    private static Dictionary<string, int> ResolveColumnIndexes(IReadOnlyDictionary<string, int> normalizedHeaders)
    {
        var resolved = new Dictionary<string, int>(StringComparer.Ordinal);
        var missingColumns = new List<string>();

        foreach (var definition in ColumnAliases)
        {
            var matchedColumn = definition.Value
                .Select(NormalizeHeader)
                .FirstOrDefault(normalizedHeaders.ContainsKey);

            if (matchedColumn is null)
            {
                if (!OptionalColumns.Contains(definition.Key))
                {
                    missingColumns.Add(definition.Key);
                }

                continue;
            }

            resolved[definition.Key] = normalizedHeaders[matchedColumn];
        }

        if (missingColumns.Count > 0)
        {
            throw new ArgumentException(
                $"The spreadsheet is missing the following required columns: {string.Join(", ", missingColumns)}.");
        }

        return resolved;
    }

    private static ImportAthleteSpreadsheetRowDto ReadRow(IXLRow row, int rowNumber,
        IReadOnlyDictionary<string, int> columnIndexes)
    {
        try
        {
            var fullName = GetRequiredText(row, columnIndexes[FullNameColumn], FullNameColumn);
            var sector = GetRequiredText(row, columnIndexes[SectorColumn], SectorColumn);
            _ = GetRequiredText(row, columnIndexes[PositionColumn], PositionColumn);
            var phase = ParsePhase(GetRequiredText(row, columnIndexes[PhaseColumn], PhaseColumn), rowNumber);
            var category = GetRequiredText(row, columnIndexes[CategoryColumn], CategoryColumn);
            var sex = ParseSex(GetRequiredText(row, columnIndexes[SexColumn], SexColumn), rowNumber);
            var ethnicity = ParseEthnicity(GetRequiredText(row, columnIndexes[EthnicityColumn], EthnicityColumn),
                rowNumber);
            var birthDate = GetRequiredDate(row, columnIndexes[BirthDateColumn], BirthDateColumn, rowNumber);
            var assessmentDate =
                GetRequiredDate(row, columnIndexes[AssessmentDateColumn], AssessmentDateColumn, rowNumber);
            var weightKg = GetRequiredDecimal(row, columnIndexes[WeightColumn], WeightColumn, rowNumber);
            var heightCm = GetRequiredDecimal(row, columnIndexes[HeightColumn], HeightColumn, rowNumber);
            var sittingHeightCm =
                GetRequiredDecimal(row, columnIndexes[SittingHeightColumn], SittingHeightColumn, rowNumber);

            var physicalAssessment = new PhysicalAssessment(
                assessmentDate,
                new GeneralMeasurementsValueObject(weightKg, heightCm, sittingHeightCm),
                new SkinfoldsValueObject(
                    GetOptionalDecimal(row, columnIndexes[RightTricepsColumn], RightTricepsColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[LeftTricepsColumn], LeftTricepsColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[SubscapularColumn], SubscapularColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[ThoraxColumn], ThoraxColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[SubaxillaryColumn], SubaxillaryColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[AbdominalColumn], AbdominalColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[RightThighColumn], RightThighColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[LeftThighColumn], LeftThighColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[RightCalfSkinfoldColumn], RightCalfSkinfoldColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[LeftCalfSkinfoldColumn], LeftCalfSkinfoldColumn, rowNumber),
                    GetOptionalColumnDecimal(row, columnIndexes, IliacCrestColumn, rowNumber),
                    GetOptionalColumnDecimal(row, columnIndexes, SupraspinaleColumn, rowNumber)),
                new CircumferencesValueObject(
                    GetOptionalDecimal(row, columnIndexes[ShoulderColumn], ShoulderColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[ChestColumn], ChestColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[RightArmColumn], RightArmColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[LeftArmColumn], LeftArmColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[WaistColumn], WaistColumn, rowNumber),
                    columnIndexes.TryGetValue(AbdominalCircumferenceColumn, out var abdominalCircumferenceIndex)
                        ? GetOptionalDecimal(row, abdominalCircumferenceIndex, AbdominalCircumferenceColumn, rowNumber)
                        : null,
                    GetOptionalDecimal(row, columnIndexes[HipColumn], HipColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[RightMidThighColumn], RightMidThighColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[LeftMidThighColumn], LeftMidThighColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[RightCalfCircumferenceColumn], RightCalfCircumferenceColumn,
                        rowNumber),
                    GetOptionalDecimal(row, columnIndexes[LeftCalfCircumferenceColumn], LeftCalfCircumferenceColumn,
                        rowNumber),
                    GetOptionalDecimal(row, columnIndexes[RightWristColumn], RightWristColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[RightKneeColumn], RightKneeColumn, rowNumber),
                    GetOptionalDecimal(row, columnIndexes[RightAnkleColumn], RightAnkleColumn, rowNumber)));

            var team = columnIndexes.TryGetValue(TeamColumn, out var teamColumnIndex)
                ? GetOptionalText(row, teamColumnIndex)
                : null;

            return new ImportAthleteSpreadsheetRowDto(
                rowNumber,
                fullName,
                sector,
                phase,
                category,
                sex,
                ethnicity,
                birthDate,
                physicalAssessment,
                team);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException($"Row {rowNumber}: {exception.Message}", exception);
        }
    }

    // A row with no name has no athlete to attach the rest of its columns to, regardless of
    // stray leftover values elsewhere (e.g. a Setor value left behind from a deleted record).
    // Skip it instead of failing the whole import over an incomplete/orphaned row.
    private static bool IsRowEmpty(IXLRow row, IReadOnlyDictionary<string, int> columnIndexes)
    {
        return string.IsNullOrWhiteSpace(GetCellText(row.Cell(columnIndexes[FullNameColumn])));
    }

    private static string GetRequiredText(IXLRow row, int columnNumber, string columnName)
    {
        var text = GetCellText(row.Cell(columnNumber));
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"Column '{columnName}' is required.");
        }

        return text.Trim();
    }

    private static string? GetOptionalText(IXLRow row, int columnNumber)
    {
        var text = GetCellText(row.Cell(columnNumber));
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static DateOnly GetRequiredDate(IXLRow row, int columnNumber, string columnName, int rowNumber)
    {
        var date = GetOptionalDate(row, columnNumber, columnName, rowNumber);
        if (date is null)
        {
            throw new ArgumentException($"Column '{columnName}' is required.");
        }

        return date.Value;
    }

    private static DateOnly? GetOptionalDate(IXLRow row, int columnNumber, string columnName, int rowNumber)
    {
        var cell = row.Cell(columnNumber);
        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue<DateTime>(out var dateTime))
        {
            return DateOnly.FromDateTime(dateTime.Date);
        }

        if (cell.TryGetValue<double>(out var oaDate))
        {
            return DateOnly.FromDateTime(DateTime.FromOADate(oaDate));
        }

        var text = GetCellText(cell);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (DateOnly.TryParse(text, PtBrCulture, DateTimeStyles.None, out var ptBrDate))
        {
            return ptBrDate;
        }

        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariantDate))
        {
            return invariantDate;
        }

        if (DateTime.TryParse(text, PtBrCulture, DateTimeStyles.None, out dateTime)
            || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
        {
            return DateOnly.FromDateTime(dateTime.Date);
        }

        throw new ArgumentException($"Column '{columnName}' has an invalid date at row {rowNumber}.");
    }

    private static decimal GetRequiredDecimal(IXLRow row, int columnNumber, string columnName, int rowNumber)
    {
        var value = GetOptionalDecimal(row, columnNumber, columnName, rowNumber);
        if (value is null)
        {
            throw new ArgumentException($"Column '{columnName}' is required.");
        }

        return value.Value;
    }

    private static decimal? GetOptionalColumnDecimal(IXLRow row, IReadOnlyDictionary<string, int> columnIndexes,
        string columnName, int rowNumber)
    {
        return columnIndexes.TryGetValue(columnName, out var columnNumber)
            ? GetOptionalDecimal(row, columnNumber, columnName, rowNumber)
            : null;
    }

    private static decimal? GetOptionalDecimal(IXLRow row, int columnNumber, string columnName, int rowNumber)
    {
        var cell = row.Cell(columnNumber);
        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue<decimal>(out var decimalValue))
        {
            return decimalValue;
        }

        if (cell.TryGetValue<double>(out var doubleValue))
        {
            return Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture);
        }

        var text = GetCellText(cell);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (string.Equals(text, "-", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "ND", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (decimal.TryParse(text, NumberStyles.Number, PtBrCulture, out decimalValue)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimalValue))
        {
            return decimalValue;
        }

        throw new ArgumentException($"Column '{columnName}' has an invalid numeric value at row {rowNumber}.");
    }

    private static string GetCellText(IXLCell cell)
    {
        var text = cell.GetFormattedString();
        text = string.IsNullOrWhiteSpace(text)
            ? cell.GetString().Trim()
            : text.Trim();

        if (SyntheticTableColumnNamePattern.IsMatch(text) && IsPhantomTableHeaderRow(cell))
        {
            return string.Empty;
        }

        return text;
    }

    private static bool IsPhantomTableHeaderRow(IXLCell cell)
    {
        var rowNumber = cell.Address.RowNumber;
        return cell.Worksheet.Tables.Any(table => table.RangeAddress.FirstAddress.RowNumber == rowNumber);
    }

    private static Phase ParsePhase(string value, int rowNumber)
    {
        return NormalizeText(value) switch
        {
            "competitive" or "competitivo" or "competitiva" => Phase.Competitive,
            "preseason" or "pretemporada" => Phase.PreSeason,
            "weightloss" or "emagrecimento" or "perdadepeso" => Phase.WeightLoss,
            "weightgain" or "ganhodepeso" => Phase.WeightGain,
            "maintenance" or "manutencao" => Phase.Maintenance,
            _ => throw new ArgumentException($"Column '{PhaseColumn}' has an unsupported value at row {rowNumber}.")
        };
    }

    private static Sex ParseSex(string value, int rowNumber)
    {
        return NormalizeText(value) switch
        {
            "male" or "masculino" or "m" or "h" => Sex.Male,
            "female" or "feminino" or "f" => Sex.Female,
            _ => throw new ArgumentException($"Column '{SexColumn}' has an unsupported value at row {rowNumber}.")
        };
    }

    private static Ethnicity ParseEthnicity(string value, int rowNumber)
    {
        return NormalizeText(value) switch
        {
            "white" or "branco" or "branca" or "b" => Ethnicity.White,
            "black" or "negro" or "negra" or "preto" or "preta" or "n" => Ethnicity.Black,
            "asian" or "asiatico" or "asiatica" or "a" => Ethnicity.Asian,
            _ => throw new ArgumentException($"Column '{EthnicityColumn}' has an unsupported value at row {rowNumber}.")
        };
    }

    private static string NormalizeHeader(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = RemoveDiacritics(value).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        var previousWasWhitespace = false;

        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasWhitespace = false;
                continue;
            }

            if (character is '.' or '-' or '/')
            {
                if (builder.Length > 0 && builder[^1] == ' ')
                {
                    builder.Length--;
                }

                builder.Append(character);
                previousWasWhitespace = false;
                continue;
            }

            if (!previousWasWhitespace)
            {
                builder.Append(' ');
                previousWasWhitespace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = RemoveDiacritics(value);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}