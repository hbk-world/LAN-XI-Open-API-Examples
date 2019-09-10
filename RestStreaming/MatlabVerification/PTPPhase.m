echo off;
close all;
clear all;

inputfile = 'LANXI_MultiModuleInputStreaming.out';

try
    samples = importdata(inputfile);
    samples = samples(1:2048,:);
    Fs = 131072;
    N = size(samples,1);
    x = (1:N)/Fs*1000;
    f = (1:(N/2)+1)*Fs/(N);

    y1 = samples(:,1)*2^-23;
    y2 = samples(:,7)*2^-23;
%         y1max = max(abs(y1));
%         y2max = max(abs(y2));
%         ymax = max([y1max y2max])*1.1;
    ymax = 1;

    fft1 = fft(y1)*2/N;
    fft2 = fft(y2)*2/N;

    cross = fft2./fft1;

    phase = angle(cross);
    phasemax = max(abs(phase))/pi*180*1.1;

    index20k = find(f==20000,1);
    [~,index20k] = min(abs(20000-f));
    f20k = f(index20k);
    phase_deg = phase(index20k)/pi*180;

    showFigure = false;
    outputFile = fopen('MatlabOutput.txt', 'w');
    if (abs(phase_deg) > 3)
%        showFigure = true;
        fprintf('Error at %dHz, phase %f\n', f20k, phase_deg);
        fprintf(outputFile, 'FAIL: Phase at %dHz: %f deg\n', f20k, phase_deg);
    elseif (abs(phase_deg) > 0.5)
%             showFigure = true;
        fprintf('Warning at %dHz, phase %f\n', f20k, phase_deg);
        fprintf(outputFile, 'PASS: Phase at %dHz: %f deg\n', f20k, phase_deg);
    else
%             showFigure = true;
        fprintf('%dHz, phase %f\n', f20k, phase_deg);
        fprintf(outputFile, 'PASS: Phase at %dHz: %f deg\n', f20k, phase_deg);
    end

    if showFigure == true
        figure;

        subplot(3,1,1);
        plot(x,y1, '-r');
        hold on;
        plot(x,y2, '--b');
        xlim([0 max(x)]);
        ylim([-ymax ymax]);
        grid;
        xlabel('ms');
        ylabel('Normalized signal');

        subplot(3,1,2);
%             semilogx(f, abs(fft1(1:(N/2)+1)), '-r');
        semilogx(f, 20*log10(abs(fft1(1:(N/2)+1))), '-r');
        hold on;
%             semilogx(f, abs(fft2(1:(N/2)+1)), '--b');
        semilogx(f, 20*log10(abs(fft2(1:(N/2)+1))), '--b');
        xlim([min(f) max(f)]);
        ylim([min(20*log10(abs(fft2(1:(N/2)+1)))) 0]);
        grid;
        xlabel('Hz');
        ylabel('dB rel FS')

        subplot(3,1,3);
        semilogx(f, phase(1:(N/2)+1)/pi*180, '-k');
        xlim([min(f) max(f)]);
        grid;
        % title('800 line FFT');
        xlabel('Hz');
        ylabel('Phase deg');
    end           
catch ME
    fprintf('Sample did not run\n');
end
