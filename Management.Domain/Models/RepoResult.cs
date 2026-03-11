namespace Management.Domain.Models
{
    public class RepoResult<T>
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public T? Data { get; set; }

        public static RepoResult<T> Success(T data) => new RepoResult<T> { IsSuccess = true, Data = data };
        public static RepoResult<T> Failure(string message) => new RepoResult<T> { IsSuccess = false, ErrorMessage = message };
    }
}
