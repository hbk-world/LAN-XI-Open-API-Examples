import matplotlib.pyplot as plt
from scipy.fftpack import fft
from scipy.signal import hilbert, correlate
import numpy as np
import cmath, os


path = 'C:\\Svn\Instrumentation\\Source\\PC\RestExamples\\RestStreaming\\CS\\PTP_MultiThreadInputStreaming\\bin\\Debug\\'
file1 = 'Master(192.168.200.98).csv'
file2 = 'Slave(192.168.200.93).csv'
column = 0              # Column number in the above files (The file must contain data from selected channel)

N = 4096                # Number of samples to read from file
fs = 65536              # Sampling frequency

 



#*****************************************************************************************
def ValidateFileNameAndPath():
#*****************************************************************************************
    if os.path.exists(path) == False:
        print("Invalid path")
        print("   " + path + "\n")
        return False

    if os.path.exists(path+file1) == False:
        print("File not found:   '" + file1 + "'\n")
        return False

    if os.path.exists(path+file2) == False:
        print("File not found:   '" + file2 + "'\n")
        return False

    return True


#*****************************************************************************************
def SignalPhaseDiff():
#*****************************************************************************************
    sigRange  = N//2
    bandwidth = fs/2                # Channel bandwidth

    signal1 = np.loadtxt(path + file1,dtype=int, delimiter=',',usecols=column,max_rows=N,skiprows=3)
    signal1 = signal1 /8388608      # 2^23=8388608

    signal2 = np.loadtxt(path + file2,dtype=int, delimiter=',',usecols=column,max_rows=N,skiprows=3)
    signal2 = signal2 /8388608      # 2^23=8388608



    t = np.linspace(0.0, bandwidth, sigRange)


    # Plot signal
    plt.subplot(311)
    plt.title('Time signal')
    plt.plot(t, signal1[0:sigRange], color='blue')
    plt.plot(t, signal2[0:sigRange], color='green')
    plt.grid()


    # Calculate phase shift using FFT method 
    plt.subplot(312)
    plt.title('FFT')
    fftSig1 = fft(signal1)/N    # fft computing and normalization
    fftSig2 = fft(signal2)/N    # fft computing  and normalization
    plt.ylim(-140,0)            # Set y-akse range
    plt.plot(t, -20*abs(np.log10(fftSig1[0:sigRange])), color='blue')   # Plot FFT signal-1
    plt.plot(t, -20*abs(np.log10(fftSig2[0:sigRange])), color='green')  # Plot FFT signal-2
    plt.grid()
  
    fftBin = np.argmax(np.abs(fftSig1))  # Find location of signal's max value (used to calculate phase diff)
    fftPhaseRad = round(np.angle(fftSig1[fftBin]/fftSig2[fftBin]),5)    # Calc and round phase-diff (radian)
    fftPhaseDeg = round(np.rad2deg(fftPhaseRad), 5)                     # Convert to degree
    print ("Phase diff (FFT)    :", fftPhaseDeg, "Deg   (",fftPhaseRad, "Rad)")



    # Calculate phase shift using Hilbert method 
    plt.subplot(313)
    plt.title('Phase diff (Hilbert)')
    plt.xlabel('f [Hz]')
    x1h = hilbert(signal1)
    x2h = hilbert(signal2) 
    c = np.inner( x1h, np.conj(x2h) ) / cmath.sqrt( np.inner(x1h,np.conj(x1h)) * np.inner(x2h,np.conj(x2h)) )
    hPhaseRad = round(abs(np.angle(c)), 5)              # Calc angle and round it
    hPhaseDeg = round(np.rad2deg(hPhaseRad), 5)         # Convert to degree
    print ("Phase diff (Hilbert):", hPhaseDeg, "Deg   (", hPhaseRad, "Rad)")
    plt.plot(t, np.angle(x1h[0:sigRange]/x2h[0:sigRange]))
    plt.grid()


    # Calculate correlation value between two signals
    corrMatrix2 = np.corrcoef(signal1,signal2)      # Correlation coeff between s1 and s2 (index (0,1) or (1,0))
    print ("Correlation value   :", round(corrMatrix2[0,1],8))


    plt.tight_layout();
    plt.show()


#*****************************************************************************************
# Main()
#*****************************************************************************************
if ValidateFileNameAndPath() == True:
    SignalPhaseDiff()
