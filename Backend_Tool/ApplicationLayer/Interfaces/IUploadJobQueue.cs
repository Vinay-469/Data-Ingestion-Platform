using CoreModels.DTOs;

namespace ApplicationLayer.Interfaces;

public interface IUploadJobQueue
{
    UploadJobStatusDto Enqueue(string userId, UploadConfirmationRequest request);
    UploadJobStatusDto? Get(Guid jobId);
    bool Cancel(string userId, Guid jobId);
}
