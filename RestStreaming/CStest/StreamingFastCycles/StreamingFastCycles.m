clear all;
close all;

runs = 100;
bw = 51200;
fs = bw*2.56;
tolerance = 0.01;
time = zeros(1,runs);
maxval = 0;

fmax = 0;
Amax = 0;

outFile = fopen('MatlabOutput.txt', 'w');

j=0;
while exist(['bin\Release\StreamingFastCycles_' num2str(j) '.out'], 'file')
    channel = load(['bin\Release\StreamingFastCycles_' num2str(j) '.out']);
%     channel = load(['bin\Release\StreamingOutputTruncation_' num2str(j) '_truncated.out']);
%     channel = signal(:,1);

    samples = size(channel,1);
%     time = (0:samples-1);%/fs;
%     figure;
%     hold on;
%     plot(time, channel);
    
    N = 4096;
    for n=0:(size(channel,1)/N)-1
        y = channel(1+N*n:N+N*n,1);
        y1 = y*2^-23;
        fftc = fft(y1)*2/N;
        fftr = 20*log10(abs(fftc(1:(N/2)+1)));
        f = 2*(1:(N/2)+1)*fs/N;
%         hold on;
%         semilogx(f,fftr);
        
        if (fmax == 0)
            fmax = find(fftr==max(fftr),1);
            Amax = fftr(fmax);
            fprintf(outFile, 'Reference at f=%d: %f\n', f(fmax), Amax);
        else
            if (fftr(fmax) < Amax-0.1 || fftr(fmax) > Amax+0.1)
                fprintf(outFile, 'Error at run %d block %d. Amplitude at f=%d: %f - expected: %f\n', j, n, f(fmax), fftr(fmax), Amax);
            else
                fprintf(outFile, 'OK at run %d block %d. Amplitude at f=%d: %f - expected: %f\n', j, n, f(fmax), fftr(fmax), Amax);
            end
        end
    end
    j = j + 1;
end
fclose(outFile);
% plot(time);