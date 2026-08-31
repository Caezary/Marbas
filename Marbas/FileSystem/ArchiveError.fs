namespace Marbas.FileSystem

type ArchiveError =
    | KindCannotBeRead of path: FileSystemPath
    | KindCannotBeChanged of path: FileSystemPath
    | ProtectedArchiveCannotBeChanged of path: FileSystemPath
    | ArchiveNotFound of path: FileSystemPath
    | ArchiveAccessDenied of path: FileSystemPath
    | PasswordNeeded of path: FileSystemPath
    | ArchiveDamaged of path: FileSystemPath * message: string
