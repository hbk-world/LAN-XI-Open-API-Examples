clear all;
close all;

N = 2;
bw = 51200;
fs = bw*2.56;
tolerance = 0.01;
time = zeros(1,N);
maxval = 0;

for j=0:N-1
    channel = load(['bin\Release\StreamingOutputTruncation_' num2str(j) '.out']);
%     channel = load(['bin\Release\StreamingOutputTruncation_' num2str(j) '_truncated.out']);
%     channel = signal(:,1);

    samples = size(channel,1);
    time = (0:samples-1);%/fs;
%     figure;
%     plot(time, channel);
    maxval = max(abs(channel));
    
%     for n=1:size(channel,1)-1
%         d = abs(channel(n+1)-channel(n));
%         if (d > 2000)
%             fprintf('Error at %d: %d\n', n, d);
%         end
%     end
    
    N = 32;
    figure;
    for n=1:(size(channel,1)/N)-1
        y = channel(1+N*n:N+N*n,1);
        a(n) = max(abs(y));
%         if (max(abs(y)) < 0.5*maxval)
%             y1 = y*2^-23;
%             fftc = fft(y1)*2/N;
%             fftr = 20*log10(abs(fftc(1:(N/2)+1)));
%             f = (1:(N/2)+1)*fs/N;
%             hold on;
%             semilogx(f,fftr);
%         end
    end
    plot(a);
    
%     for i=1:size(channel,1)-1
%         n = size(channel,1)-i;
%         if (abs(channel(n+1)) < tolerance*abs(channel(n)))
%             ['Error at sample ' num2str(n)]
%         end
%         if (channel(n) > 0.5*maxval)
%             break;
%         end
%     end

%     clear channel signal;
end
% plot(time);