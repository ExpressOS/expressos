using System;
using System.Diagnostics.Contracts;

namespace ExpressOS.Kernel
{
    /*
     * AES implementation derived from the C implementatino in OpenSSL.
     */
    public class AESManaged
    {
        public const int AES_MAXNR = 14;
        public const int AES_BLOCK_SIZE = 16;
        uint[] rd_key;
        int rounds;

        static byte[] Te4;
        static uint[] rcon;
        static ulong[] Te;
        static ulong[] Td;
        static byte[] Td4;

        public static void Initialize()
        {
            Te4 = new byte[] {
                 0x63, 0x7c, 0x77, 0x7b, 0xf2, 0x6b, 0x6f, 0xc5, 0x30, 0x01, 0x67, 0x2b, 0xfe, 0xd7, 0xab, 0x76,
                 0xca, 0x82, 0xc9, 0x7d, 0xfa, 0x59, 0x47, 0xf0, 0xad, 0xd4, 0xa2, 0xaf, 0x9c, 0xa4, 0x72, 0xc0,
                 0xb7, 0xfd, 0x93, 0x26, 0x36, 0x3f, 0xf7, 0xcc, 0x34, 0xa5, 0xe5, 0xf1, 0x71, 0xd8, 0x31, 0x15,
                 0x04, 0xc7, 0x23, 0xc3, 0x18, 0x96, 0x05, 0x9a, 0x07, 0x12, 0x80, 0xe2, 0xeb, 0x27, 0xb2, 0x75,
                 0x09, 0x83, 0x2c, 0x1a, 0x1b, 0x6e, 0x5a, 0xa0, 0x52, 0x3b, 0xd6, 0xb3, 0x29, 0xe3, 0x2f, 0x84,
                 0x53, 0xd1, 0x00, 0xed, 0x20, 0xfc, 0xb1, 0x5b, 0x6a, 0xcb, 0xbe, 0x39, 0x4a, 0x4c, 0x58, 0xcf,
                 0xd0, 0xef, 0xaa, 0xfb, 0x43, 0x4d, 0x33, 0x85, 0x45, 0xf9, 0x02, 0x7f, 0x50, 0x3c, 0x9f, 0xa8,
                 0x51, 0xa3, 0x40, 0x8f, 0x92, 0x9d, 0x38, 0xf5, 0xbc, 0xb6, 0xda, 0x21, 0x10, 0xff, 0xf3, 0xd2,
                 0xcd, 0x0c, 0x13, 0xec, 0x5f, 0x97, 0x44, 0x17, 0xc4, 0xa7, 0x7e, 0x3d, 0x64, 0x5d, 0x19, 0x73,
                 0x60, 0x81, 0x4f, 0xdc, 0x22, 0x2a, 0x90, 0x88, 0x46, 0xee, 0xb8, 0x14, 0xde, 0x5e, 0x0b, 0xdb,
                 0xe0, 0x32, 0x3a, 0x0a, 0x49, 0x06, 0x24, 0x5c, 0xc2, 0xd3, 0xac, 0x62, 0x91, 0x95, 0xe4, 0x79,
                 0xe7, 0xc8, 0x37, 0x6d, 0x8d, 0xd5, 0x4e, 0xa9, 0x6c, 0x56, 0xf4, 0xea, 0x65, 0x7a, 0xae, 0x08,
                 0xba, 0x78, 0x25, 0x2e, 0x1c, 0xa6, 0xb4, 0xc6, 0xe8, 0xdd, 0x74, 0x1f, 0x4b, 0xbd, 0x8b, 0x8a,
                 0x70, 0x3e, 0xb5, 0x66, 0x48, 0x03, 0xf6, 0x0e, 0x61, 0x35, 0x57, 0xb9, 0x86, 0xc1, 0x1d, 0x9e,
                 0xe1, 0xf8, 0x98, 0x11, 0x69, 0xd9, 0x8e, 0x94, 0x9b, 0x1e, 0x87, 0xe9, 0xce, 0x55, 0x28, 0xdf,
                 0x8c, 0xa1, 0x89, 0x0d, 0xbf, 0xe6, 0x42, 0x68, 0x41, 0x99, 0x2d, 0x0f, 0xb0, 0x54, 0xbb, 0x16
             };

            rcon = new uint[] {
                 0x00000001U, 0x00000002U, 0x00000004U, 0x00000008U,
                 0x00000010U, 0x00000020U, 0x00000040U, 0x00000080U,
                 0x0000001bU, 0x00000036U, /* for 128-bit blocks, Rijndael never uses more than 10 rcon values */
            };

            Te = new ulong[] {
                0xa56363c6a56363c6UL, 0x847c7cf8847c7cf8UL, 0x997777ee997777eeUL, 0x8d7b7bf68d7b7bf6UL,
                0x0df2f2ff0df2f2ffUL, 0xbd6b6bd6bd6b6bd6UL, 0xb16f6fdeb16f6fdeUL, 0x54c5c59154c5c591UL,
                0x5030306050303060UL, 0x0301010203010102UL, 0xa96767cea96767ceUL, 0x7d2b2b567d2b2b56UL,
                0x19fefee719fefee7UL, 0x62d7d7b562d7d7b5UL, 0xe6abab4de6abab4dUL, 0x9a7676ec9a7676ecUL,
                0x45caca8f45caca8fUL, 0x9d82821f9d82821fUL, 0x40c9c98940c9c989UL, 0x877d7dfa877d7dfaUL,
                0x15fafaef15fafaefUL, 0xeb5959b2eb5959b2UL, 0xc947478ec947478eUL, 0x0bf0f0fb0bf0f0fbUL,
                0xecadad41ecadad41UL, 0x67d4d4b367d4d4b3UL, 0xfda2a25ffda2a25fUL, 0xeaafaf45eaafaf45UL,
                0xbf9c9c23bf9c9c23UL, 0xf7a4a453f7a4a453UL, 0x967272e4967272e4UL, 0x5bc0c09b5bc0c09bUL,
                0xc2b7b775c2b7b775UL, 0x1cfdfde11cfdfde1UL, 0xae93933dae93933dUL, 0x6a26264c6a26264cUL,
                0x5a36366c5a36366cUL, 0x413f3f7e413f3f7eUL, 0x02f7f7f502f7f7f5UL, 0x4fcccc834fcccc83UL,
                0x5c3434685c343468UL, 0xf4a5a551f4a5a551UL, 0x34e5e5d134e5e5d1UL, 0x08f1f1f908f1f1f9UL,
                0x937171e2937171e2UL, 0x73d8d8ab73d8d8abUL, 0x5331316253313162UL, 0x3f15152a3f15152aUL,
                0x0c0404080c040408UL, 0x52c7c79552c7c795UL, 0x6523234665232346UL, 0x5ec3c39d5ec3c39dUL,
                0x2818183028181830UL, 0xa1969637a1969637UL, 0x0f05050a0f05050aUL, 0xb59a9a2fb59a9a2fUL,
                0x0907070e0907070eUL, 0x3612122436121224UL, 0x9b80801b9b80801bUL, 0x3de2e2df3de2e2dfUL,
                0x26ebebcd26ebebcdUL, 0x6927274e6927274eUL, 0xcdb2b27fcdb2b27fUL, 0x9f7575ea9f7575eaUL,
                0x1b0909121b090912UL, 0x9e83831d9e83831dUL, 0x742c2c58742c2c58UL, 0x2e1a1a342e1a1a34UL,
                0x2d1b1b362d1b1b36UL, 0xb26e6edcb26e6edcUL, 0xee5a5ab4ee5a5ab4UL, 0xfba0a05bfba0a05bUL,
                0xf65252a4f65252a4UL, 0x4d3b3b764d3b3b76UL, 0x61d6d6b761d6d6b7UL, 0xceb3b37dceb3b37dUL,
                0x7b2929527b292952UL, 0x3ee3e3dd3ee3e3ddUL, 0x712f2f5e712f2f5eUL, 0x9784841397848413UL,
                0xf55353a6f55353a6UL, 0x68d1d1b968d1d1b9UL, 0x0000000000000000UL, 0x2cededc12cededc1UL,
                0x6020204060202040UL, 0x1ffcfce31ffcfce3UL, 0xc8b1b179c8b1b179UL, 0xed5b5bb6ed5b5bb6UL,
                0xbe6a6ad4be6a6ad4UL, 0x46cbcb8d46cbcb8dUL, 0xd9bebe67d9bebe67UL, 0x4b3939724b393972UL,
                0xde4a4a94de4a4a94UL, 0xd44c4c98d44c4c98UL, 0xe85858b0e85858b0UL, 0x4acfcf854acfcf85UL,
                0x6bd0d0bb6bd0d0bbUL, 0x2aefefc52aefefc5UL, 0xe5aaaa4fe5aaaa4fUL, 0x16fbfbed16fbfbedUL,
                0xc5434386c5434386UL, 0xd74d4d9ad74d4d9aUL, 0x5533336655333366UL, 0x9485851194858511UL,
                0xcf45458acf45458aUL, 0x10f9f9e910f9f9e9UL, 0x0602020406020204UL, 0x817f7ffe817f7ffeUL,
                0xf05050a0f05050a0UL, 0x443c3c78443c3c78UL, 0xba9f9f25ba9f9f25UL, 0xe3a8a84be3a8a84bUL,
                0xf35151a2f35151a2UL, 0xfea3a35dfea3a35dUL, 0xc0404080c0404080UL, 0x8a8f8f058a8f8f05UL,
                0xad92923fad92923fUL, 0xbc9d9d21bc9d9d21UL, 0x4838387048383870UL, 0x04f5f5f104f5f5f1UL,
                0xdfbcbc63dfbcbc63UL, 0xc1b6b677c1b6b677UL, 0x75dadaaf75dadaafUL, 0x6321214263212142UL,
                0x3010102030101020UL, 0x1affffe51affffe5UL, 0x0ef3f3fd0ef3f3fdUL, 0x6dd2d2bf6dd2d2bfUL,
                0x4ccdcd814ccdcd81UL, 0x140c0c18140c0c18UL, 0x3513132635131326UL, 0x2fececc32fececc3UL,
                0xe15f5fbee15f5fbeUL, 0xa2979735a2979735UL, 0xcc444488cc444488UL, 0x3917172e3917172eUL,
                0x57c4c49357c4c493UL, 0xf2a7a755f2a7a755UL, 0x827e7efc827e7efcUL, 0x473d3d7a473d3d7aUL,
                0xac6464c8ac6464c8UL, 0xe75d5dbae75d5dbaUL, 0x2b1919322b191932UL, 0x957373e6957373e6UL,
                0xa06060c0a06060c0UL, 0x9881811998818119UL, 0xd14f4f9ed14f4f9eUL, 0x7fdcdca37fdcdca3UL,
                0x6622224466222244UL, 0x7e2a2a547e2a2a54UL, 0xab90903bab90903bUL, 0x8388880b8388880bUL,
                0xca46468cca46468cUL, 0x29eeeec729eeeec7UL, 0xd3b8b86bd3b8b86bUL, 0x3c1414283c141428UL,
                0x79dedea779dedea7UL, 0xe25e5ebce25e5ebcUL, 0x1d0b0b161d0b0b16UL, 0x76dbdbad76dbdbadUL,
                0x3be0e0db3be0e0dbUL, 0x5632326456323264UL, 0x4e3a3a744e3a3a74UL, 0x1e0a0a141e0a0a14UL,
                0xdb494992db494992UL, 0x0a06060c0a06060cUL, 0x6c2424486c242448UL, 0xe45c5cb8e45c5cb8UL,
                0x5dc2c29f5dc2c29fUL, 0x6ed3d3bd6ed3d3bdUL, 0xefacac43efacac43UL, 0xa66262c4a66262c4UL,
                0xa8919139a8919139UL, 0xa4959531a4959531UL, 0x37e4e4d337e4e4d3UL, 0x8b7979f28b7979f2UL,
                0x32e7e7d532e7e7d5UL, 0x43c8c88b43c8c88bUL, 0x5937376e5937376eUL, 0xb76d6ddab76d6ddaUL,
                0x8c8d8d018c8d8d01UL, 0x64d5d5b164d5d5b1UL, 0xd24e4e9cd24e4e9cUL, 0xe0a9a949e0a9a949UL,
                0xb46c6cd8b46c6cd8UL, 0xfa5656acfa5656acUL, 0x07f4f4f307f4f4f3UL, 0x25eaeacf25eaeacfUL,
                0xaf6565caaf6565caUL, 0x8e7a7af48e7a7af4UL, 0xe9aeae47e9aeae47UL, 0x1808081018080810UL,
                0xd5baba6fd5baba6fUL, 0x887878f0887878f0UL, 0x6f25254a6f25254aUL, 0x722e2e5c722e2e5cUL,
                0x241c1c38241c1c38UL, 0xf1a6a657f1a6a657UL, 0xc7b4b473c7b4b473UL, 0x51c6c69751c6c697UL,
                0x23e8e8cb23e8e8cbUL, 0x7cdddda17cdddda1UL, 0x9c7474e89c7474e8UL, 0x211f1f3e211f1f3eUL,
                0xdd4b4b96dd4b4b96UL, 0xdcbdbd61dcbdbd61UL, 0x868b8b0d868b8b0dUL, 0x858a8a0f858a8a0fUL,
                0x907070e0907070e0UL, 0x423e3e7c423e3e7cUL, 0xc4b5b571c4b5b571UL, 0xaa6666ccaa6666ccUL,
                0xd8484890d8484890UL, 0x0503030605030306UL, 0x01f6f6f701f6f6f7UL, 0x120e0e1c120e0e1cUL,
                0xa36161c2a36161c2UL, 0x5f35356a5f35356aUL, 0xf95757aef95757aeUL, 0xd0b9b969d0b9b969UL,
                0x9186861791868617UL, 0x58c1c19958c1c199UL, 0x271d1d3a271d1d3aUL, 0xb99e9e27b99e9e27UL,
                0x38e1e1d938e1e1d9UL, 0x13f8f8eb13f8f8ebUL, 0xb398982bb398982bUL, 0x3311112233111122UL,
                0xbb6969d2bb6969d2UL, 0x70d9d9a970d9d9a9UL, 0x898e8e07898e8e07UL, 0xa7949433a7949433UL,
                0xb69b9b2db69b9b2dUL, 0x221e1e3c221e1e3cUL, 0x9287871592878715UL, 0x20e9e9c920e9e9c9UL,
                0x49cece8749cece87UL, 0xff5555aaff5555aaUL, 0x7828285078282850UL, 0x7adfdfa57adfdfa5UL,
                0x8f8c8c038f8c8c03UL, 0xf8a1a159f8a1a159UL, 0x8089890980898909UL, 0x170d0d1a170d0d1aUL,
                0xdabfbf65dabfbf65UL, 0x31e6e6d731e6e6d7UL, 0xc6424284c6424284UL, 0xb86868d0b86868d0UL,
                0xc3414182c3414182UL, 0xb0999929b0999929UL, 0x772d2d5a772d2d5aUL, 0x110f0f1e110f0f1eUL,
                0xcbb0b07bcbb0b07bUL, 0xfc5454a8fc5454a8UL, 0xd6bbbb6dd6bbbb6dUL, 0x3a16162c3a16162cUL,
            };

            Td = new ulong[] {
                0x50a7f45150a7f451UL, 0x5365417e5365417eUL, 0xc3a4171ac3a4171aUL, 0x965e273a965e273aUL,
                0xcb6bab3bcb6bab3bUL, 0xf1459d1ff1459d1fUL, 0xab58faacab58faacUL, 0x9303e34b9303e34bUL,
                0x55fa302055fa3020UL, 0xf66d76adf66d76adUL, 0x9176cc889176cc88UL, 0x254c02f5254c02f5UL,
                0xfcd7e54ffcd7e54fUL, 0xd7cb2ac5d7cb2ac5UL, 0x8044352680443526UL, 0x8fa362b58fa362b5UL,
                0x495ab1de495ab1deUL, 0x671bba25671bba25UL, 0x980eea45980eea45UL, 0xe1c0fe5de1c0fe5dUL,
                0x02752fc302752fc3UL, 0x12f04c8112f04c81UL, 0xa397468da397468dUL, 0xc6f9d36bc6f9d36bUL,
                0xe75f8f03e75f8f03UL, 0x959c9215959c9215UL, 0xeb7a6dbfeb7a6dbfUL, 0xda595295da595295UL,
                0x2d83bed42d83bed4UL, 0xd3217458d3217458UL, 0x2969e0492969e049UL, 0x44c8c98e44c8c98eUL,
                0x6a89c2756a89c275UL, 0x78798ef478798ef4UL, 0x6b3e58996b3e5899UL, 0xdd71b927dd71b927UL,
                0xb64fe1beb64fe1beUL, 0x17ad88f017ad88f0UL, 0x66ac20c966ac20c9UL, 0xb43ace7db43ace7dUL,
                0x184adf63184adf63UL, 0x82311ae582311ae5UL, 0x6033519760335197UL, 0x457f5362457f5362UL,
                0xe07764b1e07764b1UL, 0x84ae6bbb84ae6bbbUL, 0x1ca081fe1ca081feUL, 0x942b08f9942b08f9UL,
                0x5868487058684870UL, 0x19fd458f19fd458fUL, 0x876cde94876cde94UL, 0xb7f87b52b7f87b52UL,
                0x23d373ab23d373abUL, 0xe2024b72e2024b72UL, 0x578f1fe3578f1fe3UL, 0x2aab55662aab5566UL,
                0x0728ebb20728ebb2UL, 0x03c2b52f03c2b52fUL, 0x9a7bc5869a7bc586UL, 0xa50837d3a50837d3UL,
                0xf2872830f2872830UL, 0xb2a5bf23b2a5bf23UL, 0xba6a0302ba6a0302UL, 0x5c8216ed5c8216edUL,
                0x2b1ccf8a2b1ccf8aUL, 0x92b479a792b479a7UL, 0xf0f207f3f0f207f3UL, 0xa1e2694ea1e2694eUL,
                0xcdf4da65cdf4da65UL, 0xd5be0506d5be0506UL, 0x1f6234d11f6234d1UL, 0x8afea6c48afea6c4UL,
                0x9d532e349d532e34UL, 0xa055f3a2a055f3a2UL, 0x32e18a0532e18a05UL, 0x75ebf6a475ebf6a4UL,
                0x39ec830b39ec830bUL, 0xaaef6040aaef6040UL, 0x069f715e069f715eUL, 0x51106ebd51106ebdUL,
                0xf98a213ef98a213eUL, 0x3d06dd963d06dd96UL, 0xae053eddae053eddUL, 0x46bde64d46bde64dUL,
                0xb58d5491b58d5491UL, 0x055dc471055dc471UL, 0x6fd406046fd40604UL, 0xff155060ff155060UL,
                0x24fb981924fb9819UL, 0x97e9bdd697e9bdd6UL, 0xcc434089cc434089UL, 0x779ed967779ed967UL,
                0xbd42e8b0bd42e8b0UL, 0x888b8907888b8907UL, 0x385b19e7385b19e7UL, 0xdbeec879dbeec879UL,
                0x470a7ca1470a7ca1UL, 0xe90f427ce90f427cUL, 0xc91e84f8c91e84f8UL, 0x0000000000000000UL,
                0x8386800983868009UL, 0x48ed2b3248ed2b32UL, 0xac70111eac70111eUL, 0x4e725a6c4e725a6cUL,
                0xfbff0efdfbff0efdUL, 0x5638850f5638850fUL, 0x1ed5ae3d1ed5ae3dUL, 0x27392d3627392d36UL,
                0x64d90f0a64d90f0aUL, 0x21a65c6821a65c68UL, 0xd1545b9bd1545b9bUL, 0x3a2e36243a2e3624UL,
                0xb1670a0cb1670a0cUL, 0x0fe757930fe75793UL, 0xd296eeb4d296eeb4UL, 0x9e919b1b9e919b1bUL,
                0x4fc5c0804fc5c080UL, 0xa220dc61a220dc61UL, 0x694b775a694b775aUL, 0x161a121c161a121cUL,
                0x0aba93e20aba93e2UL, 0xe52aa0c0e52aa0c0UL, 0x43e0223c43e0223cUL, 0x1d171b121d171b12UL,
                0x0b0d090e0b0d090eUL, 0xadc78bf2adc78bf2UL, 0xb9a8b62db9a8b62dUL, 0xc8a91e14c8a91e14UL,
                0x8519f1578519f157UL, 0x4c0775af4c0775afUL, 0xbbdd99eebbdd99eeUL, 0xfd607fa3fd607fa3UL,
                0x9f2601f79f2601f7UL, 0xbcf5725cbcf5725cUL, 0xc53b6644c53b6644UL, 0x347efb5b347efb5bUL,
                0x7629438b7629438bUL, 0xdcc623cbdcc623cbUL, 0x68fcedb668fcedb6UL, 0x63f1e4b863f1e4b8UL,
                0xcadc31d7cadc31d7UL, 0x1085634210856342UL, 0x4022971340229713UL, 0x2011c6842011c684UL,
                0x7d244a857d244a85UL, 0xf83dbbd2f83dbbd2UL, 0x1132f9ae1132f9aeUL, 0x6da129c76da129c7UL,
                0x4b2f9e1d4b2f9e1dUL, 0xf330b2dcf330b2dcUL, 0xec52860dec52860dUL, 0xd0e3c177d0e3c177UL,
                0x6c16b32b6c16b32bUL, 0x99b970a999b970a9UL, 0xfa489411fa489411UL, 0x2264e9472264e947UL,
                0xc48cfca8c48cfca8UL, 0x1a3ff0a01a3ff0a0UL, 0xd82c7d56d82c7d56UL, 0xef903322ef903322UL,
                0xc74e4987c74e4987UL, 0xc1d138d9c1d138d9UL, 0xfea2ca8cfea2ca8cUL, 0x360bd498360bd498UL,
                0xcf81f5a6cf81f5a6UL, 0x28de7aa528de7aa5UL, 0x268eb7da268eb7daUL, 0xa4bfad3fa4bfad3fUL,
                0xe49d3a2ce49d3a2cUL, 0x0d9278500d927850UL, 0x9bcc5f6a9bcc5f6aUL, 0x62467e5462467e54UL,
                0xc2138df6c2138df6UL, 0xe8b8d890e8b8d890UL, 0x5ef7392e5ef7392eUL, 0xf5afc382f5afc382UL,
                0xbe805d9fbe805d9fUL, 0x7c93d0697c93d069UL, 0xa92dd56fa92dd56fUL, 0xb31225cfb31225cfUL,
                0x3b99acc83b99acc8UL, 0xa77d1810a77d1810UL, 0x6e639ce86e639ce8UL, 0x7bbb3bdb7bbb3bdbUL,
                0x097826cd097826cdUL, 0xf418596ef418596eUL, 0x01b79aec01b79aecUL, 0xa89a4f83a89a4f83UL,
                0x656e95e6656e95e6UL, 0x7ee6ffaa7ee6ffaaUL, 0x08cfbc2108cfbc21UL, 0xe6e815efe6e815efUL,
                0xd99be7bad99be7baUL, 0xce366f4ace366f4aUL, 0xd4099fead4099feaUL, 0xd67cb029d67cb029UL,
                0xafb2a431afb2a431UL, 0x31233f2a31233f2aUL, 0x3094a5c63094a5c6UL, 0xc066a235c066a235UL,
                0x37bc4e7437bc4e74UL, 0xa6ca82fca6ca82fcUL, 0xb0d090e0b0d090e0UL, 0x15d8a73315d8a733UL,
                0x4a9804f14a9804f1UL, 0xf7daec41f7daec41UL, 0x0e50cd7f0e50cd7fUL, 0x2ff691172ff69117UL,
                0x8dd64d768dd64d76UL, 0x4db0ef434db0ef43UL, 0x544daacc544daaccUL, 0xdf0496e4df0496e4UL,
                0xe3b5d19ee3b5d19eUL, 0x1b886a4c1b886a4cUL, 0xb81f2cc1b81f2cc1UL, 0x7f5165467f516546UL,
                0x04ea5e9d04ea5e9dUL, 0x5d358c015d358c01UL, 0x737487fa737487faUL, 0x2e410bfb2e410bfbUL,
                0x5a1d67b35a1d67b3UL, 0x52d2db9252d2db92UL, 0x335610e9335610e9UL, 0x1347d66d1347d66dUL,
                0x8c61d79a8c61d79aUL, 0x7a0ca1377a0ca137UL, 0x8e14f8598e14f859UL, 0x893c13eb893c13ebUL,
                0xee27a9ceee27a9ceUL, 0x35c961b735c961b7UL, 0xede51ce1ede51ce1UL, 0x3cb1477a3cb1477aUL,
                0x59dfd29c59dfd29cUL, 0x3f73f2553f73f255UL, 0x79ce141879ce1418UL, 0xbf37c773bf37c773UL,
                0xeacdf753eacdf753UL, 0x5baafd5f5baafd5fUL, 0x146f3ddf146f3ddfUL, 0x86db447886db4478UL,
                0x81f3afca81f3afcaUL, 0x3ec468b93ec468b9UL, 0x2c3424382c342438UL, 0x5f40a3c25f40a3c2UL,
                0x72c31d1672c31d16UL, 0x0c25e2bc0c25e2bcUL, 0x8b493c288b493c28UL, 0x41950dff41950dffUL,
                0x7101a8397101a839UL, 0xdeb30c08deb30c08UL, 0x9ce4b4d89ce4b4d8UL, 0x90c1566490c15664UL,
                0x6184cb7b6184cb7bUL, 0x70b632d570b632d5UL, 0x745c6c48745c6c48UL, 0x4257b8d04257b8d0UL
            };

            Td4 = new byte[] {
                0x52, 0x09, 0x6a, 0xd5, 0x30, 0x36, 0xa5, 0x38, 0xbf, 0x40, 0xa3, 0x9e, 0x81, 0xf3, 0xd7, 0xfb,
                0x7c, 0xe3, 0x39, 0x82, 0x9b, 0x2f, 0xff, 0x87, 0x34, 0x8e, 0x43, 0x44, 0xc4, 0xde, 0xe9, 0xcb,
                0x54, 0x7b, 0x94, 0x32, 0xa6, 0xc2, 0x23, 0x3d, 0xee, 0x4c, 0x95, 0x0b, 0x42, 0xfa, 0xc3, 0x4e,
                0x08, 0x2e, 0xa1, 0x66, 0x28, 0xd9, 0x24, 0xb2, 0x76, 0x5b, 0xa2, 0x49, 0x6d, 0x8b, 0xd1, 0x25,
                0x72, 0xf8, 0xf6, 0x64, 0x86, 0x68, 0x98, 0x16, 0xd4, 0xa4, 0x5c, 0xcc, 0x5d, 0x65, 0xb6, 0x92,
                0x6c, 0x70, 0x48, 0x50, 0xfd, 0xed, 0xb9, 0xda, 0x5e, 0x15, 0x46, 0x57, 0xa7, 0x8d, 0x9d, 0x84,
                0x90, 0xd8, 0xab, 0x00, 0x8c, 0xbc, 0xd3, 0x0a, 0xf7, 0xe4, 0x58, 0x05, 0xb8, 0xb3, 0x45, 0x06,
                0xd0, 0x2c, 0x1e, 0x8f, 0xca, 0x3f, 0x0f, 0x02, 0xc1, 0xaf, 0xbd, 0x03, 0x01, 0x13, 0x8a, 0x6b,
                0x3a, 0x91, 0x11, 0x41, 0x4f, 0x67, 0xdc, 0xea, 0x97, 0xf2, 0xcf, 0xce, 0xf0, 0xb4, 0xe6, 0x73,
                0x96, 0xac, 0x74, 0x22, 0xe7, 0xad, 0x35, 0x85, 0xe2, 0xf9, 0x37, 0xe8, 0x1c, 0x75, 0xdf, 0x6e,
                0x47, 0xf1, 0x1a, 0x71, 0x1d, 0x29, 0xc5, 0x89, 0x6f, 0xb7, 0x62, 0x0e, 0xaa, 0x18, 0xbe, 0x1b,
                0xfc, 0x56, 0x3e, 0x4b, 0xc6, 0xd2, 0x79, 0x20, 0x9a, 0xdb, 0xc0, 0xfe, 0x78, 0xcd, 0x5a, 0xf4,
                0x1f, 0xdd, 0xa8, 0x33, 0x88, 0x07, 0xc7, 0x31, 0xb1, 0x12, 0x10, 0x59, 0x27, 0x80, 0xec, 0x5f,
                0x60, 0x51, 0x7f, 0xa9, 0x19, 0xb5, 0x4a, 0x0d, 0x2d, 0xe5, 0x7a, 0x9f, 0x93, 0xc9, 0x9c, 0xef,
                0xa0, 0xe0, 0x3b, 0x4d, 0xae, 0x2a, 0xf5, 0xb0, 0xc8, 0xeb, 0xbb, 0x3c, 0x83, 0x53, 0x99, 0x61,
                0x17, 0x2b, 0x04, 0x7e, 0xba, 0x77, 0xd6, 0x26, 0xe1, 0x69, 0x14, 0x63, 0x55, 0x21, 0x0c, 0x7d
            };
        }

        public AESManaged()
        {
            rd_key = new uint[4 * (AES_MAXNR + 1)];
        }

        /**
         * Expand the cipher key into the encryption key schedule.
         */
        public int SetEncryptKey(byte[] userKey, int bits)
        {
            Contract.Requires(userKey != null);
            Contract.Requires(bits == 128 || bits == 192 || bits == 256);

            int i = 0;
            int off = 0;

            var rk = rd_key;

            if (bits == 128)
                rounds = 10;
            else if (bits == 192)
                rounds = 12;
            else
                rounds = 14;

            rk[0 + off] = GetU32(userKey, 0);
            rk[1 + off] = GetU32(userKey, 4);
            rk[2 + off] = GetU32(userKey, 8);
            rk[3 + off] = GetU32(userKey, 12);


            if (bits == 128)
            {
                while (true)
                {
                    var temp = rk[3 + off];
                    rk[4 + off] = (uint)(rk[0 + off] ^
                        (Te4[(temp >> 8) & 0xff]) ^
                        (Te4[(temp >> 16) & 0xff] << 8) ^
                        (Te4[(temp >> 24)] << 16) ^
                        (Te4[(temp) & 0xff] << 24) ^
                        rcon[i]);
                    rk[5 + off] = rk[1 + off] ^ rk[4 + off];
                    rk[6 + off] = rk[2 + off] ^ rk[5 + off];
                    rk[7 + off] = rk[3 + off] ^ rk[6 + off];
                    if (++i == 10)
                    {
                        return 0;
                    }
                    off += 4;
                }
            }
            rk[4 + off] = GetU32(userKey, 16);
            rk[5 + off] = GetU32(userKey, 20);
            if (bits == 192)
            {
                while (true)
                {
                    var temp = rk[5 + off];
                    rk[6 + off] = (uint)(rk[0 + off] ^
                        (Te4[(temp >> 8) & 0xff]) ^
                        (Te4[(temp >> 16) & 0xff] << 8) ^
                        (Te4[(temp >> 24)] << 16) ^
                        (Te4[(temp) & 0xff] << 24) ^
                        rcon[i]);
                    rk[7 + off] = rk[1 + off] ^ rk[6 + off];
                    rk[8 + off] = rk[2 + off] ^ rk[7 + off];
                    rk[9 + off] = rk[3 + off] ^ rk[8 + off];
                    if (++i == 8)
                    {
                        return 0;
                    }
                    rk[10 + off] = rk[4 + off] ^ rk[9 + off];
                    rk[11 + off] = rk[5 + off] ^ rk[10 + off];
                    off += 6;
                }
            }
            rk[6 + off] = GetU32(userKey, 24);
            rk[7 + off] = GetU32(userKey, 28);
            if (bits == 256)
            {
                while (true)
                {
                    var temp = rk[7 + off];
                    rk[8 + off] = (uint)(rk[0 + off] ^
                        (Te4[(temp >> 8) & 0xff]) ^
                        (Te4[(temp >> 16) & 0xff] << 8) ^
                        (Te4[(temp >> 24)] << 16) ^
                        (Te4[(temp) & 0xff] << 24) ^
                        rcon[i]);
                    rk[9] = rk[1 + off] ^ rk[8 + off];
                    rk[10] = rk[2 + off] ^ rk[9 + off];
                    rk[11] = rk[3 + off] ^ rk[10 + off];
                    if (++i == 7)
                    {
                        return 0;
                    }
                    temp = rk[11 + off];
                    rk[12 + off] = (uint)(rk[4 + off] ^
                        (Te4[(temp) & 0xff]) ^
                        (Te4[(temp >> 8) & 0xff] << 8) ^
                        (Te4[(temp >> 16) & 0xff] << 16) ^
                        (Te4[(temp >> 24)] << 24));
                    rk[13 + off] = rk[5 + off] ^ rk[12 + off];
                    rk[14 + off] = rk[6 + off] ^ rk[13 + off];
                    rk[15 + off] = rk[7 + off] ^ rk[14 + off];

                    off += 8;
                }
            }
            return 0;
        }


        /**
         * Expand the cipher key into the decryption key schedule.
         */
        public int SetDecryptKey(byte[] userKey, int bits)
        {
            Contract.Requires(userKey != null);
            Contract.Requires(bits == 128 || bits == 192 || bits == 256);

            int i, j, status;
            uint temp;

            /* first, start with an encryption schedule */
            status = SetEncryptKey(userKey, bits);
            if (status < 0)
                return status;

            var rk = rd_key;

            /* invert the order of the round keys: */
            for (i = 0, j = 4 * rounds; i < j; i += 4, j -= 4)
            {
                temp = rk[i]; rk[i] = rk[j]; rk[j] = temp;
                temp = rk[i + 1]; rk[i + 1] = rk[j + 1]; rk[j + 1] = temp;
                temp = rk[i + 2]; rk[i + 2] = rk[j + 2]; rk[j + 2] = temp;
                temp = rk[i + 3]; rk[i + 3] = rk[j + 3]; rk[j + 3] = temp;
            }
            int off = 0;
            /* apply the inverse MixColumn transform to all round keys but the first and the last: */
            for (i = 1; i < rounds; i++)
            {
                off += 4;
                for (j = 0; j < 4; j++)
                {
                    uint tp1, tp2, tp4, tp8, tp9, tpb, tpd, tpe, m;

                    tp1 = rk[j + off];
                    m = tp1 & 0x80808080;
                    tp2 = ((tp1 & 0x7f7f7f7f) << 1) ^
                        ((m - (m >> 7)) & 0x1b1b1b1b);
                    m = tp2 & 0x80808080;
                    tp4 = ((tp2 & 0x7f7f7f7f) << 1) ^
                        ((m - (m >> 7)) & 0x1b1b1b1b);
                    m = tp4 & 0x80808080;
                    tp8 = ((tp4 & 0x7f7f7f7f) << 1) ^
                        ((m - (m >> 7)) & 0x1b1b1b1b);
                    tp9 = tp8 ^ tp1;
                    tpb = tp9 ^ tp2;
                    tpd = tp9 ^ tp4;
                    tpe = tp8 ^ tp4 ^ tp2;
                    rk[j + off] = tpe ^ (tpd >> 16) ^ (tpd << 16) ^
                        (tp9 >> 24) ^ (tp9 << 8) ^
                        (tpb >> 8) ^ (tpb << 24);
                }
            }
            return 0;
        }

        /*
         * Encrypt a single block
         * in and out can overlap
         */
        public void Encrypt(ByteBufferRef in_block, ByteBufferRef out_block)
        {
            Contract.Requires(in_block.Length >= 4 * sizeof(uint));
            Contract.Requires(out_block.Length >= 4 * sizeof(uint));

            uint s0, s1, s2, s3, t0, t1, t2, t3;
            int off = 0;
            int r;

            var rk = rd_key;

            /*
             * map byte array block to cipher state
             * and add initial round key:
             */
            s0 = GetU32(in_block, 0) ^ rk[0 + off];
            s1 = GetU32(in_block, 4) ^ rk[1 + off];
            s2 = GetU32(in_block, 8) ^ rk[2 + off];
            s3 = GetU32(in_block, 12) ^ rk[3 + off];

            t0 = Te0((s0) & 0xff) ^
                Te1((s1 >> 8) & 0xff) ^
                Te2((s2 >> 16) & 0xff) ^
                Te3((s3 >> 24)) ^
                rk[4 + off];
            t1 = Te0((s1) & 0xff) ^
                Te1((s2 >> 8) & 0xff) ^
                Te2((s3 >> 16) & 0xff) ^
                Te3((s0 >> 24)) ^
                rk[5 + off];
            t2 = Te0((s2) & 0xff) ^
                Te1((s3 >> 8) & 0xff) ^
                Te2((s0 >> 16) & 0xff) ^
                Te3((s1 >> 24)) ^
                rk[6 + off];
            t3 = Te0((s3) & 0xff) ^
                Te1((s0 >> 8) & 0xff) ^
                Te2((s1 >> 16) & 0xff) ^
                Te3((s2 >> 24)) ^
                rk[7 + off];

            s0 = t0; s1 = t1; s2 = t2; s3 = t3;

            /*
             * Nr - 2 full rounds:
             */
            for (off += 8, r = rounds - 2; r > 0; off += 4, r--)
            {

                t0 = Te0((s0) & 0xff) ^
                    Te1((s1 >> 8) & 0xff) ^
                    Te2((s2 >> 16) & 0xff) ^
                    Te3((s3 >> 24)) ^
                    rk[0 + off];
                t1 = Te0((s1) & 0xff) ^
                    Te1((s2 >> 8) & 0xff) ^
                    Te2((s3 >> 16) & 0xff) ^
                    Te3((s0 >> 24)) ^
                    rk[1 + off];
                t2 = Te0((s2) & 0xff) ^
                    Te1((s3 >> 8) & 0xff) ^
                    Te2((s0 >> 16) & 0xff) ^
                    Te3((s1 >> 24)) ^
                    rk[2 + off];
                t3 = Te0((s3) & 0xff) ^
                    Te1((s0 >> 8) & 0xff) ^
                    Te2((s1 >> 16) & 0xff) ^
                    Te3((s2 >> 24)) ^
                    rk[3 + off];
                s0 = t0; s1 = t1; s2 = t2; s3 = t3;
            }
            /*
             * apply last round and
             * map cipher state to byte array block:
             */
            SetU32(out_block, 0,
                (Te2((s0) & 0xff) & 0x000000ffU) ^
                (Te3((s1 >> 8) & 0xff) & 0x0000ff00U) ^
                (Te0((s2 >> 16) & 0xff) & 0x00ff0000U) ^
                (Te1((s3 >> 24)) & 0xff000000U) ^
                rk[0 + off]);
            SetU32(out_block, 4,
                (Te2((s1) & 0xff) & 0x000000ffU) ^
                (Te3((s2 >> 8) & 0xff) & 0x0000ff00U) ^
                (Te0((s3 >> 16) & 0xff) & 0x00ff0000U) ^
                (Te1((s0 >> 24)) & 0xff000000U) ^
                rk[1 + off]);
            SetU32(out_block, 8,
                (Te2((s2) & 0xff) & 0x000000ffU) ^
                (Te3((s3 >> 8) & 0xff) & 0x0000ff00U) ^
                (Te0((s0 >> 16) & 0xff) & 0x00ff0000U) ^
                (Te1((s1 >> 24)) & 0xff000000U) ^
                rk[2 + off]);
            SetU32(out_block, 12,
                (Te2((s3) & 0xff) & 0x000000ffU) ^
                (Te3((s0 >> 8) & 0xff) & 0x0000ff00U) ^
                (Te0((s1 >> 16) & 0xff) & 0x00ff0000U) ^
                (Te1((s2 >> 24)) & 0xff000000U) ^
                rk[3 + off]);
        }

        /*
         * Decrypt a single block
         * in and out can overlap
         */
        public void Decrypt(ByteBufferRef in_block, ByteBufferRef out_block)
        {
            Contract.Requires(in_block.Length >= 4 * sizeof(uint));
            Contract.Requires(out_block.Length >= 4 * sizeof(uint));

            uint s0, s1, s2, s3, t0, t1, t2, t3;
            int r;

            var rk = rd_key;

            /*
             * map byte array block to cipher state
             * and add initial round key:
             */
            s0 = GetU32(in_block, 0) ^ rk[0];
            s1 = GetU32(in_block, 4) ^ rk[1];
            s2 = GetU32(in_block, 8) ^ rk[2];
            s3 = GetU32(in_block, 12) ^ rk[3];

            int off = 0;

            t0 = Td0((s0) & 0xff) ^
                Td1((s3 >> 8) & 0xff) ^
                Td2((s2 >> 16) & 0xff) ^
                Td3((s1 >> 24)) ^
                rk[4 + off];
            t1 = Td0((s1) & 0xff) ^
                Td1((s0 >> 8) & 0xff) ^
                Td2((s3 >> 16) & 0xff) ^
                Td3((s2 >> 24)) ^
                rk[5 + off];
            t2 = Td0((s2) & 0xff) ^
                Td1((s1 >> 8) & 0xff) ^
                Td2((s0 >> 16) & 0xff) ^
                Td3((s3 >> 24)) ^
                rk[6 + off];
            t3 = Td0((s3) & 0xff) ^
                Td1((s2 >> 8) & 0xff) ^
                Td2((s1 >> 16) & 0xff) ^
                Td3((s0 >> 24)) ^
                rk[7 + off];
            s0 = t0; s1 = t1; s2 = t2; s3 = t3;

            /*
             * Nr - 2 full rounds:
             */
            for (off += 8, r = rounds - 2; r > 0; off += 4, r--)
            {
                t0 = Td0((s0) & 0xff) ^
                    Td1((s3 >> 8) & 0xff) ^
                    Td2((s2 >> 16) & 0xff) ^
                    Td3((s1 >> 24)) ^
                    rk[0 + off];
                t1 = Td0((s1) & 0xff) ^
                    Td1((s0 >> 8) & 0xff) ^
                    Td2((s3 >> 16) & 0xff) ^
                    Td3((s2 >> 24)) ^
                    rk[1 + off];
                t2 = Td0((s2) & 0xff) ^
                    Td1((s1 >> 8) & 0xff) ^
                    Td2((s0 >> 16) & 0xff) ^
                    Td3((s3 >> 24)) ^
                    rk[2 + off];
                t3 = Td0((s3) & 0xff) ^
                    Td1((s2 >> 8) & 0xff) ^
                    Td2((s1 >> 16) & 0xff) ^
                    Td3((s0 >> 24)) ^
                    rk[3 + off];
                s0 = t0; s1 = t1; s2 = t2; s3 = t3;
            }
            /*
             * apply last round and
             * map cipher state to byte array block:
             */

            SetU32(out_block, 0, (uint)(
                (Td4[(s0) & 0xff]) ^
                (Td4[(s3 >> 8) & 0xff] << 8) ^
                (Td4[(s2 >> 16) & 0xff] << 16) ^
                (Td4[(s1 >> 24)] << 24) ^
                rk[0 + off]));

            SetU32(out_block, 4, (uint)(
                (Td4[(s1) & 0xff]) ^
                (Td4[(s0 >> 8) & 0xff] << 8) ^
                (Td4[(s3 >> 16) & 0xff] << 16) ^
                (Td4[(s2 >> 24)] << 24) ^
                rk[1 + off]));
            SetU32(out_block, 8, (uint)(
                (Td4[(s2) & 0xff]) ^
                (Td4[(s1 >> 8) & 0xff] << 8) ^
                (Td4[(s0 >> 16) & 0xff] << 16) ^
                (Td4[(s3 >> 24)] << 24) ^
                rk[2 + off]));
            SetU32(out_block, 12, (uint)(
                (Td4[(s3) & 0xff]) ^
                (Td4[(s2 >> 8) & 0xff] << 8) ^
                (Td4[(s1 >> 16) & 0xff] << 16) ^
                (Td4[(s0 >> 24)] << 24) ^
                rk[3 + off]));
        }

        private static uint GetU32(ByteBufferRef b, int offset)
        {
            return Deserializer.ReadUInt(b, offset);
        }

        private static uint GetU32(byte[] b, int offset)
        {
            return Deserializer.ReadUInt(b, offset);
        }

        private static void SetU32(ByteBufferRef arr, int off, uint val)
        {
            Deserializer.WriteUInt(val, arr, off);
        }

        private static uint Te0(uint index)
        {
            return (uint)Te[index];
        }

        private static uint Te1(uint index)
        {
            return (uint)(Te[index] >> 24);
        }

        private static uint Te2(uint index)
        {
            return (uint)(Te[index] >> 16);
        }

        private static uint Te3(uint index)
        {
            return (uint)(Te[index] >> 8);
        }

        private static uint Td0(uint index)
        {
            return (uint)Td[index];
        }

        private static uint Td1(uint index)
        {
            return (uint)(Td[index] >> 24);
        }

        private static uint Td2(uint index)
        {
            return (uint)(Td[index] >> 16);
        }

        private static uint Td3(uint index)
        {
            return (uint)(Td[index] >> 8);
        }
    }
}
