#ifndef EXPRESSOS_PRINTK_H_
#define EXPRESSOS_PRINTK_H_

void vcon_setup(void);
int printk(const char *fmt, ...) __attribute__((format (printf, 1, 2)));

#endif
