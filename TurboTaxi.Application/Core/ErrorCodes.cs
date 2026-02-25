using System.ComponentModel;

namespace TurboTaxi.Application.Core
{
    public enum ErrorCodes
    {
        [Description("Validation error")] 
        VALIDATION_ERROR = 1000,
        [Description("Internal server error")] 
        INTERNAL_SERVER_ERROR = 2000
    }
}
