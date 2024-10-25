using FluentValidation.Results;

namespace VizInvoiceGeneratorWebAPI.Services.Extensions
{
    public static class ErrorService
    {
        public static object GetErorList(string[] errorTypes, string[] errorMessages)
        {

            var errors = new List<object>();
            for (int i=0; i< errorTypes.Length; i++)
            {
                var errorObj = new
                {
                    type = errorTypes[i],
                    message = errorMessages[i]
                };
                errors.Add(errorObj);
            }

            return new
            {
                error = errors
            };
        }
        public static string GetErrorMessage(ValidationResult result)
        {
            string errMessage = "";
            foreach (var failure in result.Errors)
            {
                errMessage = errMessage == "" ? failure.ErrorMessage : errMessage + ", " + failure.ErrorMessage;
            }
            return errMessage;
        }
    }
}
