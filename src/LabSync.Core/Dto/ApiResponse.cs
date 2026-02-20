namespace LabSync.Core.Dto
{
    /// <summary>
    /// A generic API response object for returning simple messages.
    /// </summary>
    public class ApiResponse
    {
        /// <summary>
        /// The message to be returned to the client.
        /// </summary>
        public string Message { get; set; }

        public ApiResponse(string message)
        {
            Message = message;
        }
    }
}
