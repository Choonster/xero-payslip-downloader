using System.Text.Json.Serialization;

namespace XeroPayslipDownloader.DTO
{
    public record XeroPayRunHistoryListDTO(
        IList<XeroPayRunHistoryDTO> Data,
        string Message,
        string MessageCategory,
        string MessageType,
        int RecordCount
    );

    public record XeroPayRunHistoryDTO(
        int PaySlipID,
        int PayeeID,
        string PayeeName,
        [property: JsonConverter(typeof(JsonMicrosoftDateTimeConverter))] DateTime CalendarReferenceDate,
        XeroPeriodDTO Period
    );

    public record XeroPeriodDTO(
        string Description,
        [property: JsonConverter(typeof(JsonMicrosoftDateTimeConverter))] DateTime StartDate,
        [property: JsonConverter(typeof(JsonMicrosoftDateTimeConverter))] DateTime EndDate,
        int SequenceNumber
    );
}
