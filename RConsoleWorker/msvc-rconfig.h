/* Visual Studio's C compiler and R 4.6's headers.  The headers declare Rboolean with a C23 enum base type, which that compiler
 * does not accept, and Rcomplex with a C complex member.  Forced in before every include, this takes Rconfig.h first and
 * withdraws the enum base type: a plain enum is int-sized, as enum : int is.  The build defines R_LEGACY_RCOMPLEX for the
 * struct form of Rcomplex, of the same layout; the worker uses no complex values.  The pinned Zig needs neither. */
#include <Rconfig.h>
#undef HAVE_ENUM_BASE_TYPE
