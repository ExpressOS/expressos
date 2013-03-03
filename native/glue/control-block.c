#include "expressos/expressos-native.h"

int linux_pending_reply_count(void)
{
	volatile int count = g_expressos_control_block->pending_reply_count;
        return count; 
}
