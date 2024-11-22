using System;

namespace Topup.Shared;

public interface IAudit
{
    DateTime CreatedDate { get; set; }
    DateTime? ModifiedDate { get; set; }
    string ModifiedBy { get; set; }
    string CreatedBy { get; set; }
}

public interface IMustHaveTenant
{
    int TenantId { get; set; }
}

public interface IMayHaveTenant
{
    int? TenantId { get; set; }
}

public interface IHasCreationTime
{
    DateTime CreationTime { get; set; }
}

public interface IHasModificationTime
{
    DateTime? LastModificationTime { get; set; }
}

public interface IModificationAudited : IHasModificationTime
{
    long? LastModifierUserId { get; set; }
}

public interface ICreationAudited : IHasCreationTime
{
    long? CreatorUserId { get; set; }
}

public interface IAudited : ICreationAudited, IHasCreationTime, IModificationAudited, IHasModificationTime
{
}

public interface IRequestAudit
{
    long RequestUserId { get; set; }
    string RequestUserName { get; set; }
}

public interface IAuditRequest
{
    int TenantId { get; set; }
    string RequestUserName { get; set; }
}