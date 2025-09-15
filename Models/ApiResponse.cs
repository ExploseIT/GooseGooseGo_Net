

namespace GooseGooseGo_Net.Models
{
    public class ApiResponse<T>
    {
        public bool apiSuccess { get; set; } = true;
        public string apiMessage { get; set; } = "";
        public T? apiData { get; set; } = default;

        public ApiResponse() { }

        public ApiResponse(T _data, string _message = "", bool _success = true)
        {
            apiData = _data;
            apiMessage = _message;
            apiSuccess = _success;
        }

    }

}
