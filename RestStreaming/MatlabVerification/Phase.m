close all;
clear all;

samples = importdata('LANXI_MultiModuleInputStreaming.out');
samples = samples(1:2048,:);

Fs = 131072;
N = size(samples,1);
x = (1:N)/Fs*1000;
f = (1:(N/2)+1)*Fs/(N);

y1 = samples(:,1)*2^-23;
y2 = samples(:,7)*2^-23;
y1max = max(abs(y1));
y2max = max(abs(y2));
ymax = max([y1max y2max])*1.1;

fft1 = fft(y1);
fft2 = fft(y2);

cross = fft2./fft1;

phase = angle(cross);
phasemax = abs(max(phase))/pi*180*1.1;

figure;
subplot(3,1,1);
plot(x,y1, '-r');
xlabel('ms');
hold on;
plot(x,y2, '--b');
xlim([0 max(x)]);
ylim([-ymax ymax]);
subplot(3,1,2);
semilogx(f, abs(fft1(1:(N/2)+1)), '-r');
hold on;
semilogx(f, abs(fft2(1:(N/2)+1)), '--b');
xlim([20 51200]);
subplot(3,1,3);
semilogx(f, phase(1:(N/2)+1)/pi*180, '-k');
xlim([20 51200]);
xlabel('Hz');
grid;
% title('800 line FFT');
xlabel('Hz')
ylabel('dB rel FS')
grid;