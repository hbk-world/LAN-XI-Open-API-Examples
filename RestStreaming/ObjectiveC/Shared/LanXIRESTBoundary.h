//
//  LanXIRESTBoundary.h
//  LanXIInterface
//
//  Created by Per Boye Clausen on 16/09/13.
//  Copyright (c) 2013 Brüel & Kjær. All rights reserved.
//

#import <Foundation/Foundation.h>

@interface LanXIRESTBoundary : NSObject

/**
    Hostname to send requests to using this object. May be a hostname or an IP address.
 */
@property (nonatomic,readonly) NSString *host;

- (id) initWithHostname:(NSString *)host;

- (NSJSONSerialization*) requestWithPath:(NSString *)path
                                 method:(NSString *)method
                                   body:(NSData *)body
                                  error:(NSError**)error;

- (NSJSONSerialization*) getRequestWithPath:(NSString *)path
                                      error:(NSError**)error;

- (NSJSONSerialization*) putRequestWithPath:(NSString *)path
                                       body:(NSData *)body
                                      error:(NSError**)error;

- (NSJSONSerialization*) postRequestWithPath:(NSString *)path
                                        body:(NSData *)body
                                       error:(NSError**)error;

- (BOOL) waitForRecorderState:(NSString *)state error:(NSError **)error;

- (BOOL) waitForPtpState:(NSString *)state error:(NSError **)error;

- (BOOL) waitForInputState:(NSString *)state error:(NSError **)error;

@end
