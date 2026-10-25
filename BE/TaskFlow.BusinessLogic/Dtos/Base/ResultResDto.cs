namespace TaskFlow.BusinessLogic.Dtos.Base
{
    public class ResultResDto<T> : ExecutionResDto
    {
        public T? Result { get; set; }
    }
}
