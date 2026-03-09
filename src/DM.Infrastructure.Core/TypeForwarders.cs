// Type forwarders for types migrated to DM.Domain.Core
// These ensure backward compatibility for code still using old namespaces

using System.Runtime.CompilerServices;

// Dto types moved to DM.Domain.Core.Dto
[assembly: TypeForwardedTo(typeof(DM.Domain.Core.Dto.GeneralUser))]
[assembly: TypeForwardedTo(typeof(DM.Domain.Core.Dto.IUser))]

// Abstractions moved to DM.Domain.Core.Abstractions
[assembly: TypeForwardedTo(typeof(DM.Domain.Core.Abstractions.IDateTimeProvider))]
[assembly: TypeForwardedTo(typeof(DM.Domain.Core.Abstractions.IGuidFactory))]
