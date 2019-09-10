clear all;
close all;

% N = 10;
bw = 51200;
fs = bw*2.56;
tolerance = 0.1;
% time = zeros(1,N);
maxval = 0;
maxtol = 0;

j=0;
l=1;
while exist(['bin\Release\StreamingOutputDelay_' num2str(j) '.out'], 'file')
% for j=0:N-1
    j
    channel = load(['bin\Release\StreamingOutputDelay_' num2str(j) '.out']);
%     channel = signal(:,1);

    if (maxval == 0)
        maxval = max(abs(channel));
        maxtol = maxval*tolerance;
    end
    silence = 0;

    for i=1:100:size(channel,1)
        if (abs(channel(i)) > maxtol)
            % Possible signal found. Verify that it is not just a short
            % spike. At least 70 of the next 100 samples should be above
            % the tolerance to count as a signal.
            count = 0;
            for k=0:49
                if (abs(channel(i+k)) > maxtol)
                    count = count + 1;
                end
            end
            if (count > 35)
                silence = i;
                break;
            end
        end
    end
%     time(j+1) = silence;%/fs
    if (silence ~= 0)
        time(l) = silence;%/fs
        l = l+1;
    end
%     figure;
%     plot(channel);
%     clear channel signal;
    j = j+1;
end
% figure;
% plot(time);
outFile = fopen('MatlabOutput.txt', 'w');
fprintf(outFile, 'Count: %d recordings\n', size(time,2));
fprintf(outFile, 'Delays /samples:\n');
for i=1:size(time,2)
    fprintf(outFile, '%d\t', time(i));
end
fprintf(outFile, '\n');
fprintf(outFile, 'Max: %d samples / %fs, Min: %d samples / %fs\n', max(time), max(time)/fs, min(time), min(time)/fs);
diff = max(time)-min(time);
fprintf(outFile, 'Diff: %d samples / %fs\n', diff, diff/fs);
meanval = mean(time);
fprintf(outFile, 'Mean: %d samples / %fs\n', meanval, meanval/fs);
fclose(outFile);