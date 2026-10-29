/* Decodes a FLAC file from memory with the engine's dr_flac copy (same defines as audiopp) to raw int16:
 *   "S161" | int32 channels | int32 rate | int32 frames | int16 interleaved samples
 * Usage: flacdec in.flac out.raw
 */
#define DR_FLAC_IMPLEMENTATION
#define DR_FLAC_NO_STDIO
#define DR_FLAC_NO_SIMD
#include "C:/Workspace/github/UnravelEngine/UnravelEngine/deps/audiopp/audiopp/audiopp/loaders/decoders/decoder_flac.h"
#include <stdio.h>
#include <stdlib.h>

int main(int argc, char** argv)
{
    if(argc < 3)
    {
        return 2;
    }
    FILE* f = fopen(argv[1], "rb");
    if(!f)
    {
        return 2;
    }
    fseek(f, 0, SEEK_END);
    long size = ftell(f);
    fseek(f, 0, SEEK_SET);
    unsigned char* data = (unsigned char*)malloc((size_t)size);
    fread(data, 1, (size_t)size, f);
    fclose(f);
    unsigned int channels = 0;
    unsigned int rate = 0;
    drflac_uint64 frames = 0;
    drflac_int16* pcm = drflac_open_memory_and_read_pcm_frames_s16(data, (size_t)size, &channels, &rate, &frames);
    if(!pcm)
    {
        fprintf(stderr, "decode failed\n");
        return 1;
    }
    FILE* out = fopen(argv[2], "wb");
    int header[3] = {(int)channels, (int)rate, (int)frames};
    fwrite("S161", 1, 4, out);
    fwrite(header, 4, 3, out);
    fwrite(pcm, sizeof(drflac_int16), (size_t)(frames * channels), out);
    fclose(out);
    printf("%u ch, %u Hz, %llu frames\n", channels, rate, (unsigned long long)frames);
    return 0;
}
